mod activation;
#[cfg(target_os = "macos")]
mod app_menu;
mod credentials;
mod links;
mod sidecar;
mod sidecar_process;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let builder = tauri::Builder::default();
    #[cfg(target_os = "macos")]
    let builder = builder
        .menu(app_menu::with_graceful_quit)
        .on_menu_event(app_menu::handle_event);
    let app = builder
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            activation::request(app);
        }))
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
        if matches!(event, tauri::RunEvent::Ready) {
            activation::ready(app);
        }
        if let tauri::RunEvent::ExitRequested { api, .. } = event {
            if sidecar::request_shutdown(app.clone()) {
                api.prevent_exit();
            }
        }
    });
}
