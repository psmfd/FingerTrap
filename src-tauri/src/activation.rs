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
    if app
        .try_state::<crate::sidecar::SidecarState>()
        .is_some_and(|state| state.is_shutting_down())
    {
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
    if let Err(error) = app.show() {
        eprintln!("could not show application: {error}");
    }
    for result in [window.show(), window.unminimize(), window.set_focus()] {
        if let Err(error) = result {
            eprintln!("could not activate existing window: {error}");
        }
    }
}
