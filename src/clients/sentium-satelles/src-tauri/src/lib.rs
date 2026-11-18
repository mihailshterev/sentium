//! Sentium Satelles: runs in the notification area (menu bar on macOS), reports this PC's health to Vigil, shows the
//! owner's Notifications and syncs their Obsidian vault one way to Sentium. The connection is always opened by the PC,
//! and the server can never make it do anything beyond showing a notification; the vault is only ever read.

mod agent;
mod api;
mod icon;
mod links;
mod metrics;
mod store;
mod toast;
mod tray;
mod vault;
mod vault_files;

use std::sync::{Arc, OnceLock};

use tauri::{AppHandle, Emitter, Manager, RunEvent, State, WindowEvent};
use tauri_plugin_autostart::MacosLauncher;
use tauri_plugin_dialog::DialogExt;
use tauri_plugin_opener::OpenerExt;
use url::Url;

use agent::{Agent, Observer, Snapshot};
use api::{Api, Notification};
use store::{Store, VaultSettings};
use tray::{Commands, Tray};

const MAIN_WINDOW: &str = "main";
const STATE_EVENT: &str = "satelles-state";
const DEVICE_NAME_MAX: usize = 64;
/// Every Obsidian vault has this folder; requiring it keeps a mis-click from syncing a whole home folder.
const OBSIDIAN_FOLDER: &str = ".obsidian";

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

/// Asks for the vault folder and starts syncing it; `false` when the person cancelled the dialog.
#[tauri::command]
async fn choose_vault_folder(app: AppHandle, state: State<'_, AppState>) -> Result<bool, String> {
    let (sender, receiver) = tokio::sync::oneshot::channel();
    app.dialog()
        .file()
        .set_title("Choose your Obsidian vault folder")
        .pick_folder(move |folder| {
            let _ = sender.send(folder);
        });

    let Some(folder) = receiver.await.map_err(|e| e.to_string())? else {
        return Ok(false);
    };
    let path = folder.into_path().map_err(|e| e.to_string())?;

    if !path.join(OBSIDIAN_FOLDER).is_dir() {
        return Err("That folder is not an Obsidian vault (it has no .obsidian folder). Choose the vault's own folder.".into());
    }

    let name = path
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| "Vault".into());
    let vault = state.agent.vault();
    let excludes = vault.settings().map(|v| v.excludes).unwrap_or_default();

    vault.configure(Some(VaultSettings {
        path: path.to_string_lossy().into_owned(),
        name,
        excludes,
        paused: false,
    }))?;
    Ok(true)
}

/// `excludes`: one glob per line.
#[tauri::command]
fn set_vault_excludes(state: State<'_, AppState>, excludes: String) -> Result<(), String> {
    let vault = state.agent.vault();
    let Some(settings) = vault.settings() else {
        return Err("Choose a vault folder first.".into());
    };

    let excludes = excludes
        .lines()
        .map(str::trim)
        .filter(|line| !line.is_empty())
        .map(String::from)
        .collect();
    vault.configure(Some(VaultSettings {
        excludes,
        ..settings
    }))
}

#[tauri::command]
fn set_vault_paused(state: State<'_, AppState>, paused: bool) -> Result<(), String> {
    let vault = state.agent.vault();
    let Some(settings) = vault.settings() else {
        return Err("Choose a vault folder first.".into());
    };
    vault.configure(Some(VaultSettings { paused, ..settings }))
}

#[tauri::command]
fn take_over_vault(state: State<'_, AppState>) {
    state.agent.vault().take_over();
}

#[tauri::command]
fn sync_vault_now(state: State<'_, AppState>) {
    state.agent.vault().sync_now();
}

/// Stops syncing and forgets the folder. The notes already on the server stay until removed in the portal.
#[tauri::command]
fn stop_vault_sync(state: State<'_, AppState>) -> Result<(), String> {
    state.agent.vault().configure(None)
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
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            get_state,
            default_device_name,
            pair,
            hide_window,
            choose_vault_folder,
            set_vault_excludes,
            set_vault_paused,
            take_over_vault,
            sync_vault_now,
            stop_vault_sync
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
