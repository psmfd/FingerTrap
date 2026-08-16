//! Shell-owned credential storage (ADR-0022).
//!
//! The Rust shell is the only durable holder of provider tokens: it is the
//! signed binary macOS Keychain ACLs bind to, so grants survive updates once
//! N-2 signing lands, and the one identity the operator's trust decision is
//! about. Tokens reach the .NET sidecar only as a `credentials/set` JSON-RPC
//! *notification* written into its stdin — notifications have no response
//! frame, and sidecar stdout is relayed wholesale to the WebView, so no
//! secret-bearing frame can ever travel toward the WebView.
//!
//! Fail-closed: if the platform store is unavailable (headless Linux without
//! a Secret Service is the realistic case), status reports
//! `store-unavailable` and nothing is ever written to a plaintext fallback.

use keyring::v1::Entry;
use tauri::State;
use zeroize::Zeroizing;

use crate::sidecar::SidecarState;

/// Keychain service name — the app identifier, so items are recognizably
/// FingerTrap's in Keychain Access / Credential Manager / seahorse.
const SERVICE: &str = "dev.fingertrap.app";

/// The only providers a token may be stored for. A typo'd provider must be
/// an error, not a stranded keychain item (same posture as ADR-0013's
/// unrecognized pane kind).
const PROVIDERS: [&str; 2] = ["github", "ado"];

/// Fine-grained PATs and ADO PATs are well under this; anything larger is
/// not a token.
const MAX_TOKEN_LEN: usize = 512;

fn validate_provider(provider: &str) -> Result<(), String> {
    if PROVIDERS.contains(&provider) {
        Ok(())
    } else {
        Err(format!(
            "unknown provider '{provider}'; expected one of: {}",
            PROVIDERS.join(", ")
        ))
    }
}

fn entry_for(provider: &str) -> Result<Entry, String> {
    // Error text from keyring never contains the secret; safe to surface.
    Entry::new(SERVICE, provider).map_err(|e| format!("credential store error: {e}"))
}

/// Builds the `credentials/set` notification frame (ADR-0002 framing).
/// `None` clears the provider sidecar-side.
fn credentials_frame(provider: &str, token: Option<&str>) -> Zeroizing<Vec<u8>> {
    let body = serde_json::json!({
        "jsonrpc": "2.0",
        "method": "credentials/set",
        "params": { "provider": provider, "token": token },
    })
    .to_string();
    let frame = format!("Content-Length: {}\r\n\r\n{}", body.len(), body);
    Zeroizing::new(frame.into_bytes())
}

fn push_to_sidecar(
    state: &State<'_, SidecarState>,
    provider: &str,
    token: Option<&str>,
) -> Result<(), String> {
    let frame = credentials_frame(provider, token);
    // Never log the frame or attach it to an error — the token is inside it.
    state
        .write(&frame)
        .map_err(|_| "failed to deliver credential to sidecar".to_string())
}

/// Store a token in the OS keychain and deliver it to the running sidecar.
/// The WebView calls this once with operator-pasted input and never sees the
/// token again — there is deliberately no `credential_get`.
#[tauri::command]
pub fn credential_save(
    state: State<'_, SidecarState>,
    provider: String,
    token: String,
) -> Result<(), String> {
    let token = Zeroizing::new(token);
    validate_provider(&provider)?;
    if token.is_empty() || token.len() > MAX_TOKEN_LEN {
        return Err(format!("token must be 1..={MAX_TOKEN_LEN} characters"));
    }
    if token.chars().any(char::is_control) {
        return Err("token contains control characters".into());
    }

    entry_for(&provider)?
        .set_password(&token)
        .map_err(|e| format!("credential store error: {e}"))?;
    push_to_sidecar(&state, &provider, Some(&token))
}

/// Remove a provider's token from the keychain and clear it sidecar-side.
#[tauri::command]
pub fn credential_clear(state: State<'_, SidecarState>, provider: String) -> Result<(), String> {
    validate_provider(&provider)?;
    match entry_for(&provider)?.delete_credential() {
        Ok(()) => {}
        // Already absent is success — same idempotency contract as pty/kill.
        Err(keyring::v1::Error::NoEntry) => {}
        Err(e) => return Err(format!("credential store error: {e}")),
    }
    push_to_sidecar(&state, &provider, None)
}

/// `configured` | `not-configured` | `store-unavailable` — the UI renders
/// states, never blanks (ADR-0022), and the token itself never crosses back.
#[tauri::command]
pub fn credential_status(provider: String) -> Result<String, String> {
    validate_provider(&provider)?;
    match entry_for(&provider)?.get_password() {
        Ok(secret) => {
            drop(Zeroizing::new(secret));
            Ok("configured".into())
        }
        Err(keyring::v1::Error::NoEntry) => Ok("not-configured".into()),
        Err(_) => Ok("store-unavailable".into()),
    }
}

/// Called after (re)spawn: push every stored credential into the fresh
/// sidecar. The sidecar holds tokens in memory only, so a respawn starts
/// empty until this runs.
pub fn preload_into_sidecar(state: &State<'_, SidecarState>) {
    for provider in PROVIDERS {
        let entry = match entry_for(provider) {
            Ok(entry) => entry,
            Err(e) => {
                eprintln!("credential preload skipped ({provider}): {e}");
                continue;
            }
        };
        match entry.get_password() {
            Ok(secret) => {
                let secret = Zeroizing::new(secret);
                if let Err(e) = push_to_sidecar(state, provider, Some(&secret)) {
                    eprintln!("credential preload failed ({provider}): {e}");
                }
            }
            Err(keyring::v1::Error::NoEntry) => {}
            Err(e) => {
                // Store unavailable (e.g. no Secret Service): fail closed and
                // loud, feature degrades to not-configured sidecar-side.
                eprintln!("credential preload skipped ({provider}): {e}");
            }
        }
    }
}
