use std::collections::HashSet;
use std::io::Read;
use std::sync::atomic::{AtomicU8, Ordering};
use std::sync::{Arc, Condvar, Mutex};
use std::time::Duration;

use crate::sidecar_process::{finish_shutdown, SidecarProcess, WriteReceipt};
use tauri::{ipc::Channel, Manager, State};
use tauri_plugin_shell::ShellExt;

// Covers the sidecar RPC disposer (10 s + 5 s drain), PTY cleanup (2 s),
// and scheduling overhead. Keep this outer deadline above the inner budgets.
const SHUTDOWN_GRACE: Duration = Duration::from_secs(20);
const SHUTDOWN_PAYLOAD: &[u8] = br#"{"jsonrpc":"2.0","method":"shutdown"}"#;
const SHUTDOWN_RUNNING: u8 = 0;
const SHUTDOWN_WAITING: u8 = 1;
const SHUTDOWN_ALLOW_EXIT: u8 = 2;

pub struct SidecarState {
    child: Mutex<Option<SidecarProcess>>,
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
    pub fn enqueue(&self, payload: &[u8]) -> Result<WriteReceipt, String> {
        if self.shutdown_phase.load(Ordering::Acquire) != SHUTDOWN_RUNNING {
            return Err("sidecar is shutting down".into());
        }
        let guard = self.child.lock().unwrap();
        match guard.as_ref() {
            Some(child) => child.enqueue(payload),
            None => Err("sidecar is not running".into()),
        }
    }

    pub fn write(&self, payload: &[u8]) -> Result<(), String> {
        self.enqueue(payload)?.wait()
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

    // Clone the OS process handle before starting the watchdog. Neither this
    // lock nor SharedChild's kill path is held by the pipe writer.
    let child = state
        .child
        .lock()
        .unwrap()
        .as_ref()
        .map(|p| Arc::clone(&p.child));
    let terminated = Arc::clone(&state.terminated);
    let shutdown_handle = app_handle.clone();
    std::thread::spawn(move || {
        match finish_shutdown(child.as_deref(), &terminated, SHUTDOWN_GRACE) {
            Ok(true) => eprintln!("sidecar shutdown grace expired; forced direct child exit"),
            Ok(false) => {}
            Err(error) => eprintln!("failed to force sidecar exit: {error}"),
        }

        shutdown_handle
            .state::<SidecarState>()
            .shutdown_phase
            .store(SHUTDOWN_ALLOW_EXIT, Ordering::Release);
        shutdown_handle.exit(0);
    });

    // Non-blocking admission only. If the queue is already full, the watchdog
    // still runs and kills the stalled sidecar at the deadline.
    if let Some(process) = state.child.lock().unwrap().as_ref() {
        if let Err(error) = process.enqueue(&shutdown_frame()) {
            eprintln!("failed to queue graceful sidecar shutdown: {error}");
        }
    }

    true
}

pub fn spawn(app: &mut tauri::App) -> Result<(), Box<dyn std::error::Error>> {
    let app_handle = app.handle().clone();
    // Keep Tauri's sidecar path resolution, environment and Windows flags.
    // Own the pipes separately so stdin backpressure cannot lock out kill().
    let mut command: std::process::Command = app.shell().sidecar("fingertrap-sidecar")?.into();
    let process = SidecarProcess::spawn(&mut command)?;
    let mut stdout = process
        .child
        .take_stdout()
        .ok_or("missing sidecar stdout")?;
    let mut stderr = process
        .child
        .take_stderr()
        .ok_or("missing sidecar stderr")?;
    let child = Arc::clone(&process.child);

    let state: State<SidecarState> = app_handle.state();
    state
        .shutdown_phase
        .store(SHUTDOWN_RUNNING, Ordering::Release);
    {
        let (lock, _) = &*state.terminated;
        *lock.lock().unwrap() = false;
    }
    *state.child.lock().unwrap() = Some(process);

    // The sidecar holds tokens in memory only; every (re)spawn starts empty
    // until the shell re-pushes what the keychain holds (ADR-0022). Runs on
    // a blocking task, never inline in setup: the keychain read can hang on
    // a modal ACL prompt, and window/pane bring-up must not wait on it
    // (#105).
    let preload_handle = app_handle.clone();
    tauri::async_runtime::spawn_blocking(move || {
        crate::credentials::preload_into_sidecar(&preload_handle.state::<SidecarState>());
    });

    let output_handle = app_handle.clone();
    std::thread::spawn(move || {
        let mut buffer = [0; 16 * 1024];
        while let Ok(count) = stdout.read(&mut buffer) {
            if count == 0 {
                break;
            }
            let state: State<SidecarState> = output_handle.state();
            let guard = state.output_channel.lock().unwrap();
            if let Some(channel) = guard.as_ref() {
                let _ = channel.send(buffer[..count].to_vec());
            }
        }
    });
    std::thread::spawn(move || {
        let mut buffer = [0; 4096];
        while let Ok(count) = stderr.read(&mut buffer) {
            if count == 0 {
                break;
            }
            eprintln!(
                "sidecar stderr: {}",
                String::from_utf8_lossy(&buffer[..count])
            );
        }
    });
    // Exit observation does not wait for descendants to close inherited pipes.
    std::thread::spawn(move || match child.wait() {
        Ok(status) => {
            eprintln!("sidecar terminated: {status}");
            app_handle.state::<SidecarState>().mark_terminated();
        }
        Err(error) => eprintln!("sidecar wait failed: {error}"),
    });

    Ok(())
}

#[tauri::command]
pub async fn sidecar_write(state: State<'_, SidecarState>, payload: Vec<u8>) -> Result<(), String> {
    let receipt = state.enqueue(&payload)?;
    tauri::async_runtime::spawn_blocking(move || receipt.wait())
        .await
        .map_err(|_| "sidecar input worker failed".to_string())?
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
