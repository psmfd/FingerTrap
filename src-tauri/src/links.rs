//! Second gate of the ADR-0023 URL-open policy. The sidecar validated a
//! row's URL at construction, but the WebView is untrusted by definition —
//! a compromised renderer must not be able to open arbitrary URLs by
//! skipping the sidecar. Re-run the same checks here, then hand off to the
//! opener plugin's Rust API. The plugin's own JS surface is never granted
//! in capabilities/, so this command is the only path.

use tauri::Url;
use tauri_plugin_opener::OpenerExt;

/// Exact-match host allowlist, kept in lockstep (by test, not shared code)
/// with `StatusUrls` in the sidecar.
const ALLOWED_HOSTS: [&str; 2] = ["github.com", "dev.azure.com"];

fn is_allowed(candidate: &str) -> bool {
    let Ok(url) = Url::parse(candidate) else {
        return false;
    };
    url.scheme() == "https"
        && url.username().is_empty()
        && url.password().is_none()
        && url
            .host_str()
            .is_some_and(|host| ALLOWED_HOSTS.iter().any(|a| host.eq_ignore_ascii_case(a)))
}

#[tauri::command]
pub fn open_url(app: tauri::AppHandle, url: String) -> Result<(), String> {
    if !is_allowed(&url) {
        return Err("url is not on the allowed-host list".into());
    }
    app.opener()
        .open_url(url, None::<&str>)
        .map_err(|e| e.to_string())
}

#[cfg(test)]
mod tests {
    use super::is_allowed;

    #[test]
    fn allows_https_on_allowlisted_hosts() {
        assert!(is_allowed(
            "https://github.com/psmfd/FingerTrap/actions/runs/1"
        ));
        assert!(is_allowed(
            "https://dev.azure.com/org/proj/_workitems/edit/7"
        ));
        assert!(is_allowed("https://GITHUB.COM/psmfd/FingerTrap"));
    }

    #[test]
    fn rejects_everything_else() {
        assert!(!is_allowed("http://github.com/psmfd"));
        assert!(!is_allowed("https://evil.example/github.com"));
        assert!(!is_allowed("https://github.com.evil.example/x"));
        assert!(!is_allowed("https://user@github.com/x"));
        assert!(!is_allowed("https://github.com@evil.example/x"));
        assert!(!is_allowed("file:///etc/passwd"));
        assert!(!is_allowed("javascript:alert(1)"));
        assert!(!is_allowed("not a url"));
        assert!(!is_allowed(""));
    }
}
