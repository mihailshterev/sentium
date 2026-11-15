//! Sentium Satelles: runs in the notification area (menu bar on macOS), reports this PC's health to Vigil and shows the
//! owner's Notifications. The connection is always opened by the PC, and the server can never make it do anything
//! beyond showing a notification.

mod agent;
mod api;
mod icon;
mod links;
mod metrics;
mod store;
mod toast;
mod tray;

use std::sync::{Arc, OnceLock};

use tauri::{AppHandle, Emitter, Manager, RunEvent, State, WindowEvent};
use tauri_plugin_autostart::MacosLauncher;
use tauri_plugin_opener::OpenerExt;
use url::Url;

use agent::{Agent, Observer, Snapshot};
use api::{Api, Notification};
use store::Store;
use tray::{Commands, Tray};

const MAIN_WINDOW: &str = "main";
const STATE_EVENT: &str = "satelles-state";
const DEVICE_NAME_MAX: usize = 64;

struct AppState {
    agent: Arc<Agent>,
}

/// Mirrors the agent onto the tray, the window and toasts. The tray is created after the agent, hence the cell.
struct AppObserver {
    app: AppHandle,
    tray: Arc<OnceLock<Tray>>,
}

impl Observer for AppObserver {
    fn state_changed(&self, snapshot: Snapshot) {
        if let Some(tray) = self.tray.get() {
            tray.update(&snapshot);
        }
        let _ = self.app.emit(STATE_EVENT, snapshot);
    }

    fn notification(&self, server: &Url, notification: Notification) {
        let target = links::resolve_notification_link(server, notification.link.as_deref());
        toast::show(
            &self.app,
            &notification.title,
            notification.body.as_deref(),
            Some(target),
        );
    }
}

struct TrayCommands;

impl Commands for TrayCommands {
    fn open_sentium(&self, app: &AppHandle) {
        match app.state::<AppState>().agent.server() {
            Some(server) => {
                if let Err(error) = app.opener().open_url(server.as_str(), None::<&str>) {
                    log::warn!("could not open Sentium: {error}");
                }
            }
            None => show_window(app),
        }
    }

    fn show_window(&self, app: &AppHandle) {
        show_window(app);
    }

    fn unpair(&self, app: &AppHandle) {
        let app = app.clone();
        tauri::async_runtime::spawn(async move {
            let removed_on_server = app.state::<AppState>().agent.unpair().await;
            if !removed_on_server {
                toast::show(
                    &app,
                    "Unpaired on this PC only",
                    Some(
                        "The server could not be reached. Remove this device under My devices in the portal.",
                    ),
                    None,
                );
            }
            show_window(&app);
        });
    }

    fn exit(&self, app: &AppHandle) {
        app.state::<AppState>().agent.shutdown();
        app.exit(0);
    }
}

#[tauri::command]
fn get_state(state: State<'_, AppState>) -> Snapshot {
    state.agent.snapshot()
}

#[tauri::command]
fn default_device_name() -> String {
    let host = sysinfo::System::host_name().unwrap_or_else(|_| "This PC".into());
    let user = std::env::var("USERNAME")
        .or_else(|_| std::env::var("USER"))
        .unwrap_or_default();
    let name = if user.is_empty() {
        host
    } else {
        format!("{host} ({user})")
    };
    name.chars().take(DEVICE_NAME_MAX).collect()
}

#[tauri::command]
async fn pair(
    state: State<'_, AppState>,
    server: String,
    code: String,
    name: String,
) -> Result<(), String> {
    let server = links::parse_server(&server)?;
    let code = code.trim();
    let name: String = name.trim().chars().take(DEVICE_NAME_MAX).collect();
    if code.is_empty() || name.is_empty() {
        return Err("Enter the pairing code and a name for this device.".into());
    }

    state.agent.pair(server, code, &name).await
}

#[tauri::command]
fn hide_window(app: AppHandle) {
    if let Some(window) = app.get_webview_window(MAIN_WINDOW) {
        let _ = window.hide();
    }
}

fn show_window(app: &AppHandle) {
    if let Some(window) = app.get_webview_window(MAIN_WINDOW) {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

pub fn run() {
    let app = tauri::Builder::default()
        // Must be the first plugin: a second launch hands over to the running app and exits.
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            show_window(app)
        }))
        .plugin(tauri_plugin_autostart::init(
            MacosLauncher::LaunchAgent,
            None,
        ))
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            get_state,
            default_device_name,
            pair,
            hide_window
        ])
        .setup(|app| {
            // Lives in the menu bar: no Dock icon on macOS.
            #[cfg(target_os = "macos")]
            app.set_activation_policy(tauri::ActivationPolicy::Accessory);

            let handle = app.handle().clone();
            let tray_cell = Arc::new(OnceLock::new());
            let observer = Arc::new(AppObserver {
                app: handle.clone(),
                tray: Arc::clone(&tray_cell),
            });

            let version = app.package_info().version.to_string();
            let agent = Agent::new(
                Api::new(&version),
                Store::new(app.path().app_local_data_dir()?),
                version,
                observer,
            );

            let tray = Tray::build(&handle, TrayCommands)?;
            tray.update(&agent.snapshot());
            let _ = tray_cell.set(tray);

            app.manage(AppState {
                agent: Arc::clone(&agent),
            });
            agent.start();

            if !agent.is_paired() {
                show_window(&handle);
            }

            Ok(())
        })
        .on_window_event(|window, event| {
            // Closing the window only hides it: the app lives on in the tray icon until Exit.
            if let WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .build(tauri::generate_context!())
        .expect("error while building Sentium Satelles");

    app.run(|_app, event| {
        if let RunEvent::ExitRequested { api, code, .. } = event {
            // Hiding the last window must not end the app; only the Exit command (with a code) does.
            if code.is_none() {
                api.prevent_exit();
            }
        }
    });
}
