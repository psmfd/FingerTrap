use std::collections::HashSet;
use std::sync::atomic::{AtomicU8, Ordering};
use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

use tauri::{ipc::Channel, Manager, State};
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

const SHUTDOWN_GRACE: Duration = Duration::from_secs(8);
const SHUTDOWN_PAYLOAD: &[u8] = br#"{"jsonrpc":"2.0","method":"shutdown"}"#;
const SHUTDOWN_RUNNING: u8 = 0;
const SHUTDOWN_WAITING: u8 = 1;
const SHUTDOWN_ALLOW_EXIT: u8 = 2;

pub struct SidecarState {
    child: Mutex<Option<CommandChild>>,
    output_channel: Mutex<Option<Channel<Vec<u8>>>>,
    /// Providers whose sidecar-side credential state an operator command has
    /// already set. The async preload (#105) must never clobber these: a
    /// preload read stuck behind a keychain modal could otherwise land after
    /// a save/clear and push a stale token — or resurrect a cleared one.
    credential_overrides: Mutex<HashSet<String>>,
    terminated: Arc<(Mutex<bool>, Condvar)>,
    shutdown_phase: AtomicU8,
}

impl Default for SidecarState {
    fn default() -> Self {
        Self {
            child: Mutex::new(None),
            output_channel: Mutex::new(None),
            credential_overrides: Mutex::new(HashSet::new()),
            terminated: Arc::new((Mutex::new(false), Condvar::new())),
            shutdown_phase: AtomicU8::new(SHUTDOWN_RUNNING),
        }
    }
}

impl SidecarState {
    /// Write raw bytes into the sidecar's stdin. Callers own framing; this
    /// deliberately never logs the payload (credentials/set frames carry
    /// secrets — ADR-0022).
    pub fn write(&self, payload: &[u8]) -> Result<(), String> {
        let mut guard = self.child.lock().unwrap();
        match guard.as_mut() {
            Some(child) => child.write(payload).map_err(|e| e.to_string()),
            None => Err("sidecar is not running".into()),
        }
    }

    /// Operator-command credential write: records the provider as
    /// operator-owned, then writes. Holds the overrides lock across the
    /// write so it serializes against `write_credential_preload` — whichever
    /// path runs second sees a consistent state, and the operator's write
    /// always wins (#105).
    pub fn write_credential_override(&self, provider: &str, frame: &[u8]) -> Result<(), String> {
        let mut overrides = self.credential_overrides.lock().unwrap();
        overrides.insert(provider.to_string());
        self.write(frame)
    }

    /// Preload credential write: skipped (successfully) when an operator
    /// command already set this provider's state. Holds the overrides lock
    /// across the write — see `write_credential_override`.
    pub fn write_credential_preload(&self, provider: &str, frame: &[u8]) -> Result<(), String> {
        let overrides = self.credential_overrides.lock().unwrap();
        if overrides.contains(provider) {
            return Ok(());
        }
        self.write(frame)
    }

    fn mark_terminated(&self) {
        let (lock, ready) = &*self.terminated;
        *lock.lock().unwrap() = true;
        ready.notify_all();
    }
}

fn shutdown_frame() -> Vec<u8> {
    let mut frame = format!("Content-Length: {}\r\n\r\n", SHUTDOWN_PAYLOAD.len()).into_bytes();
    frame.extend_from_slice(SHUTDOWN_PAYLOAD);
    frame
}

#[derive(Debug, PartialEq, Eq)]
enum ShutdownDecision {
    Start,
    Wait,
    Exit,
}

fn shutdown_decision(state: &SidecarState) -> ShutdownDecision {
    match state.shutdown_phase.compare_exchange(
        SHUTDOWN_RUNNING,
        SHUTDOWN_WAITING,
        Ordering::AcqRel,
        Ordering::Acquire,
    ) {
        Ok(_) => ShutdownDecision::Start,
        Err(SHUTDOWN_WAITING) => ShutdownDecision::Wait,
        Err(_) => ShutdownDecision::Exit,
    }
}

/// Intercept native app exit and ask the sidecar to reap its own process
/// trees. Every external duplicate is prevented while cleanup is in progress;
/// only the final `AppHandle::exit` is allowed through.
pub fn request_shutdown(app_handle: tauri::AppHandle) -> bool {
    let state: State<SidecarState> = app_handle.state();
    match shutdown_decision(&state) {
        ShutdownDecision::Wait => return true,
        ShutdownDecision::Exit => return false,
        ShutdownDecision::Start => {}
    }

    if let Err(error) = state.write(&shutdown_frame()) {
        eprintln!("failed to request graceful sidecar shutdown: {error}");
    }

    let terminated = Arc::clone(&state.terminated);
    std::thread::spawn(move || {
        let (lock, ready) = &*terminated;
        let guard = lock.lock().unwrap();
        let (guard, wait) = ready
            .wait_timeout_while(guard, SHUTDOWN_GRACE, |done| !*done)
            .unwrap();

        if !*guard && wait.timed_out() {
            eprintln!("sidecar shutdown grace expired; forcing direct child exit");
            let state: State<SidecarState> = app_handle.state();
            let child = state.child.lock().unwrap().take();
            if let Some(child) = child {
                if let Err(error) = child.kill() {
                    eprintln!("failed to force sidecar exit: {error}");
                }
            }
        }

        app_handle
            .state::<SidecarState>()
            .shutdown_phase
            .store(SHUTDOWN_ALLOW_EXIT, Ordering::Release);
        app_handle.exit(0);
    });

    true
}

pub fn spawn(app: &mut tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let app_handle = app.handle().clone();
    // set_raw_out: deliver stdout as raw bytes. The default reader splits on
    // \r or \n, which fragments LSP-style JSON-RPC framing
    // (`Content-Length: N\r\n\r\n{...}`) and pty/output payloads that
    // contain CR/LF inside the JSON body.
    let (mut rx, child) = app
        .shell()
        .sidecar("fingertrap-sidecar")?
        .set_raw_out(true)
        .spawn()?;

    let state: State<SidecarState> = app_handle.state();
    state
        .shutdown_phase
        .store(SHUTDOWN_RUNNING, Ordering::Release);
    {
        let (lock, _) = &*state.terminated;
        *lock.lock().unwrap() = false;
    }
    *state.child.lock().unwrap() = Some(child);

    // The sidecar holds tokens in memory only; every (re)spawn starts empty
    // until the shell re-pushes what the keychain holds (ADR-0022). Runs on
    // a blocking task, never inline in setup: the keychain read can hang on
    // a modal ACL prompt, and window/pane bring-up must not wait on it
    // (#105).
    let preload_handle = app_handle.clone();
    tauri::async_runtime::spawn_blocking(move || {
        crate::credentials::preload_into_sidecar(&preload_handle.state::<SidecarState>());
    });

    tauri::async_runtime::spawn(async move {
        while let Some(event) = rx.recv().await {
            match event {
                CommandEvent::Stdout(bytes) => {
                    let state: State<SidecarState> = app_handle.state();
                    let guard = state.output_channel.lock().unwrap();
                    if let Some(channel) = guard.as_ref() {
                        let _ = channel.send(bytes);
                    }
                }
                CommandEvent::Stderr(bytes) => {
                    eprintln!("sidecar stderr: {}", String::from_utf8_lossy(&bytes));
                }
                CommandEvent::Terminated(payload) => {
                    eprintln!("sidecar terminated: {:?}", payload);
                    app_handle.state::<SidecarState>().mark_terminated();
                    break;
                }
                CommandEvent::Error(message) => {
                    eprintln!("sidecar error: {message}");
                }
                _ => {}
            }
        }
    });

    Ok(())
}

#[tauri::command]
pub fn sidecar_write(state: State<'_, SidecarState>, payload: Vec<u8>) -> Result<(), String> {
    state.write(&payload)
}

#[tauri::command]
pub fn subscribe_sidecar_output(
    state: State<'_, SidecarState>,
    channel: Channel<Vec<u8>>,
) -> Result<(), String> {
    *state.output_channel.lock().unwrap() = Some(channel);
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn shutdown_frame_is_one_complete_json_rpc_notification() {
        assert_eq!(
            shutdown_frame(),
            b"Content-Length: 37\r\n\r\n{\"jsonrpc\":\"2.0\",\"method\":\"shutdown\"}"
        );
    }

    #[test]
    fn duplicate_exit_requests_wait_until_the_internal_exit_is_allowed() {
        let state = SidecarState::default();

        assert_eq!(shutdown_decision(&state), ShutdownDecision::Start);
        assert_eq!(shutdown_decision(&state), ShutdownDecision::Wait);
        state
            .shutdown_phase
            .store(SHUTDOWN_ALLOW_EXIT, Ordering::Release);
        assert_eq!(shutdown_decision(&state), ShutdownDecision::Exit);
    }
}
