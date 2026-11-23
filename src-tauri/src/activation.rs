//! Repeated launches activate the existing app without changing its sessions.
use std::sync::atomic::{AtomicBool, Ordering};
use tauri::Manager;

static PENDING: AtomicBool = AtomicBool::new(false);

pub fn request(app: &tauri::AppHandle) {
    PENDING.store(true, Ordering::Release);
    let handle = app.clone();
    if let Err(error) = app.run_on_main_thread(move || activate(&handle)) {
        eprintln!("could not schedule window activation: {error}");
    }
}

pub fn ready(app: &tauri::AppHandle) {
    if PENDING.load(Ordering::Acquire) {
        activate(app);
    }
}

fn activate(app: &tauri::AppHandle) {
    if shutting_down(app) {
        PENDING.store(false, Ordering::Release);
        return;
    }
    let Some(window) = app.get_webview_window("main") else {
        // A concurrent launch can arrive before the first window exists.
        // Ready will retry; never panic or create a second sidecar here.
        return;
    };
    PENDING.store(false, Ordering::Release);

    #[cfg(target_os = "macos")]
    macos::restore_and_focus(app, window);
    #[cfg(not(target_os = "macos"))]
    for result in [window.show(), window.unminimize(), window.set_focus()] {
        report(result);
    }
}

fn shutting_down(app: &tauri::AppHandle) -> bool {
    app.try_state::<crate::sidecar::SidecarState>()
        .is_some_and(|state| state.is_shutting_down())
}

fn report(result: tauri::Result<()>) {
    if let Err(error) = result {
        eprintln!("could not activate existing window: {error}");
    }
}

#[cfg(target_os = "macos")]
mod macos {
    use super::*;
    use std::time::{Duration, Instant};
    use tauri::WebviewWindow;

    const RESTORE_TIMEOUT: Duration = Duration::from_secs(2);
    const RESTORE_INTERVAL: Duration = Duration::from_millis(50);
    static RESTORE_PENDING: AtomicBool = AtomicBool::new(false);

    pub(super) fn restore_and_focus(app: &tauri::AppHandle, window: WebviewWindow) {
        if RESTORE_PENDING.swap(true, Ordering::AcqRel) {
            return;
        }
        report(app.show());
        report(window.show());
        report(window.unminimize());
        focus_when_restored(app.clone(), window, Instant::now() + RESTORE_TIMEOUT);
    }

    fn focus_when_restored(app: tauri::AppHandle, window: WebviewWindow, deadline: Instant) {
        if shutting_down(&app) || Instant::now() >= deadline {
            RESTORE_PENDING.store(false, Ordering::Release);
            return;
        }
        // Tao skips focus while AppKit still reports the window as minimized
        // or invisible, including during the Dock's restore animation.
        if ready_to_focus(window.is_minimized(), window.is_visible()) {
            report(window.set_focus());
            RESTORE_PENDING.store(false, Ordering::Release);
            return;
        }
        // Wait off the main thread. Only one poll is outstanding, and each
        // poll checks the Quit gate again before touching the window.
        tauri::async_runtime::spawn_blocking(move || {
            std::thread::sleep(RESTORE_INTERVAL);
            let handle = app.clone();
            if let Err(error) = app.run_on_main_thread(move || {
                focus_when_restored(handle, window, deadline);
            }) {
                report(Err(error));
                RESTORE_PENDING.store(false, Ordering::Release);
            }
        });
    }

    fn ready_to_focus(minimized: tauri::Result<bool>, visible: tauri::Result<bool>) -> bool {
        matches!((minimized, visible), (Ok(false), Ok(true)))
    }

    #[cfg(test)]
    mod tests {
        use super::*;

        #[test]
        fn focus_waits_for_a_visible_restored_window() {
            assert!(!ready_to_focus(Ok(true), Ok(false)));
            assert!(!ready_to_focus(Ok(true), Ok(true)));
            assert!(!ready_to_focus(Ok(false), Ok(false)));
            assert!(ready_to_focus(Ok(false), Ok(true)));
        }
    }
}
