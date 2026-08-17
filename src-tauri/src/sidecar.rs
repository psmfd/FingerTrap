use std::collections::HashSet;
use std::sync::Mutex;

use tauri::{ipc::Channel, Manager, State};
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

#[derive(Default)]
pub struct SidecarState {
    child: Mutex<Option<CommandChild>>,
    output_channel: Mutex<Option<Channel<Vec<u8>>>>,
    /// Providers whose sidecar-side credential state an operator command has
    /// already set. The async preload (#105) must never clobber these: a
    /// preload read stuck behind a keychain modal could otherwise land after
    /// a save/clear and push a stale token — or resurrect a cleared one.
    credential_overrides: Mutex<HashSet<String>>,
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
