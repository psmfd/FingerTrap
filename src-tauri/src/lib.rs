mod credentials;
mod links;
mod sidecar;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_opener::init())
        .manage(sidecar::SidecarState::default())
        .setup(|app| {
            sidecar::spawn(app)?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            sidecar::sidecar_write,
            sidecar::subscribe_sidecar_output,
            credentials::credential_save,
            credentials::credential_clear,
            credentials::credential_status,
            links::open_url,
        ])
        .build(tauri::generate_context!())
        .expect("error while building tauri application");

    app.run(|app, event| {
        if let tauri::RunEvent::ExitRequested { api, .. } = event {
            if sidecar::request_shutdown(app.clone()) {
                api.prevent_exit();
            }
        }
    });
}
