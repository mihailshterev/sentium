//! The tray (menu bar on macOS) icon and its menu.

use tauri::menu::{CheckMenuItem, Menu, MenuItem, PredefinedMenuItem};
use tauri::tray::{MouseButton, MouseButtonState, TrayIcon, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Wry};
use tauri_plugin_autostart::ManagerExt;

use crate::agent::Snapshot;
use crate::icon;

const OPEN: &str = "open";
const PAIR: &str = "pair";
const UNPAIR: &str = "unpair";
const AUTOSTART: &str = "autostart";
const EXIT: &str = "exit";

/// Handles to the menu entries whose text or visibility follows the agent's state.
pub struct Tray {
    icon: TrayIcon,
    status: MenuItem<Wry>,
    unread: MenuItem<Wry>,
    pair: MenuItem<Wry>,
    unpair: MenuItem<Wry>,
}

pub trait Commands: Send + Sync + 'static {
    fn open_sentium(&self, app: &AppHandle);
    fn show_window(&self, app: &AppHandle);
    fn unpair(&self, app: &AppHandle);
    fn exit(&self, app: &AppHandle);
}

impl Tray {
    pub fn build<C: Commands>(app: &AppHandle, commands: C) -> tauri::Result<Self> {
        let status = MenuItem::with_id(app, "status", "Not paired", false, None::<&str>)?;
        let unread = MenuItem::with_id(app, "unread", "", false, None::<&str>)?;
        let open = MenuItem::with_id(app, OPEN, "Open Sentium", true, None::<&str>)?;
        let pair = MenuItem::with_id(app, PAIR, "Pair this PC…", true, None::<&str>)?;
        let unpair = MenuItem::with_id(app, UNPAIR, "Unpair", true, None::<&str>)?;
        let autostart_enabled = app.autolaunch().is_enabled().unwrap_or(false);
        let autostart = CheckMenuItem::with_id(app, AUTOSTART, "Start at sign-in", true, autostart_enabled, None::<&str>)?;
        let exit = MenuItem::with_id(app, EXIT, "Exit", true, None::<&str>)?;

        let menu = Menu::with_items(
            app,
            &[
                &status,
                &unread,
                &PredefinedMenuItem::separator(app)?,
                &open,
                &pair,
                &unpair,
                &autostart,
                &PredefinedMenuItem::separator(app)?,
                &exit,
            ],
        )?;

        let commands = std::sync::Arc::new(commands);
        let menu_commands = std::sync::Arc::clone(&commands);
        let autostart_item = autostart.clone();

        let icon = TrayIconBuilder::with_id("sentium")
            .icon(icon::render(crate::agent::Status::NotPaired, false))
            .tooltip("Sentium")
            .menu(&menu)
            .show_menu_on_left_click(false)
            .on_menu_event(move |app, event| match event.id().as_ref() {
                OPEN => menu_commands.open_sentium(app),
                PAIR => menu_commands.show_window(app),
                UNPAIR => menu_commands.unpair(app),
                AUTOSTART => toggle_autostart(app, &autostart_item),
                EXIT => menu_commands.exit(app),
                _ => {}
            })
            .on_tray_icon_event(move |tray, event| {
                if let TrayIconEvent::Click { button: MouseButton::Left, button_state: MouseButtonState::Up, .. } = event {
                    commands.open_sentium(tray.app_handle());
                }
            })
            .build(app)?;

        Ok(Self { icon, status, unread, pair, unpair })
    }

    pub fn update(&self, snapshot: &Snapshot) {
        let unread_text = match snapshot.unread {
            1 => "1 unread notification".to_string(),
            n => format!("{n} unread notifications"),
        };
        let tooltip = if snapshot.paired && snapshot.unread > 0 {
            format!("Sentium · {} unread", snapshot.unread)
        } else {
            format!("Sentium · {}", snapshot.status_text)
        };

        let results = [
            self.status.set_text(&snapshot.status_text),
            self.unread.set_text(if snapshot.paired { unread_text.as_str() } else { "" }),
            self.pair.set_enabled(!snapshot.paired),
            self.unpair.set_enabled(snapshot.paired),
            self.icon.set_icon(Some(icon::render(snapshot.status, snapshot.paired && snapshot.unread > 0))),
            self.icon.set_tooltip(Some(tooltip)),
        ];

        for error in results.into_iter().filter_map(Result::err) {
            log::warn!("could not update the tray: {error}");
        }
    }
}

fn toggle_autostart(app: &AppHandle, item: &CheckMenuItem<Wry>) {
    let launcher = app.autolaunch();
    let result = if launcher.is_enabled().unwrap_or(false) { launcher.disable() } else { launcher.enable() };
    if let Err(error) = result {
        log::warn!("could not change the autostart setting: {error}");
    }

    let _ = item.set_checked(launcher.is_enabled().unwrap_or(false));
}
