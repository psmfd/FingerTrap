//! Route the macOS app menu's Quit action through Tauri's exit request.
use tauri::menu::{Menu, MenuEvent, MenuItem, MenuItemKind, PredefinedMenuItem};
use tauri::AppHandle;

const QUIT_ID: &str = "fingertrap-quit";

pub(super) fn with_graceful_quit(app: &AppHandle) -> tauri::Result<Menu<tauri::Wry>> {
    let menu = Menu::default(app)?;
    let menus = menu.items()?;
    let app_menu = menus
        .first()
        .and_then(MenuItemKind::as_submenu)
        .ok_or_else(|| std::io::Error::other("missing default macOS application menu"))?;

    // In the default macOS application menu the last item is native Quit.
    // Keep its title and position, replacing the AppKit terminate: action.
    let items = app_menu.items()?;
    let native_quit = items
        .last()
        .and_then(MenuItemKind::as_predefined_menuitem)
        .ok_or_else(|| std::io::Error::other("missing default macOS Quit menu item"))?;
    let title = native_quit.text()?;
    if title != PredefinedMenuItem::quit(app, None)?.text()? {
        return Err(std::io::Error::other("default macOS Quit menu item moved").into());
    }
    let quit = MenuItem::with_id(app, QUIT_ID, title, true, Some("Cmd+Q"))?;
    app_menu.remove(native_quit)?;
    app_menu.append(&quit)?;
    Ok(menu)
}

pub(super) fn handle_event(app: &AppHandle, event: MenuEvent) {
    if event.id() == QUIT_ID {
        app.exit(0);
    }
}
