//! Native notifications that report a click, one implementation per platform. The Tauri notification plugin has no
//! click events on desktop, which a toast that opens its portal page needs.

use tauri::AppHandle;
use url::Url;

/// Shows a notification; clicking it opens `target`, if any, in the default browser.
pub fn show(app: &AppHandle, title: &str, body: Option<&str>, target: Option<Url>) {
    platform::show(app, title, body.unwrap_or_default(), target);
}

fn open(app: &AppHandle, target: Option<&Url>) {
    use tauri_plugin_opener::OpenerExt;

    let Some(target) = target else {
        return;
    };

    if let Err(error) = app.opener().open_url(target.as_str(), None::<&str>) {
        log::warn!("could not open {target}: {error}");
    }
}

#[cfg(windows)]
mod platform {
    use tauri::AppHandle;
    use tauri_winrt_notification::Toast;
    use url::Url;

    /// Windows shows toasts only for a registered app id. The installer registers the bundle identifier on the Start
    /// menu shortcut; a development build borrows PowerShell's.
    pub fn show(app: &AppHandle, title: &str, body: &str, target: Option<Url>) {
        let app_id = if cfg!(debug_assertions) {
            Toast::POWERSHELL_APP_ID.to_string()
        } else {
            app.config().identifier.clone()
        };
        let handle = app.clone();

        let result = Toast::new(&app_id)
            .title(title)
            .text1(body)
            .on_activated(move |_| {
                super::open(&handle, target.as_ref());
                Ok(())
            })
            .show();

        if let Err(error) = result {
            log::warn!("could not show a toast: {error}");
        }
    }
}

#[cfg(target_os = "macos")]
mod platform {
    use std::sync::Once;

    use mac_notification_sys::{Notification, NotificationResponse};
    use tauri::AppHandle;
    use url::Url;

    static REGISTER: Once = Once::new();

    /// Sending blocks until the person reacts, so it runs on a blocking thread.
    pub fn show(app: &AppHandle, title: &str, body: &str, target: Option<Url>) {
        let identifier = app.config().identifier.clone();
        REGISTER.call_once(|| {
            if let Err(error) = mac_notification_sys::set_application(&identifier) {
                log::warn!("could not register for notifications: {error}");
            }
        });

        let (handle, title, body) = (app.clone(), title.to_string(), body.to_string());
        tauri::async_runtime::spawn_blocking(move || {
            match Notification::new()
                .title(&title)
                .message(&body)
                .wait_for_click(true)
                .send()
            {
                Ok(NotificationResponse::Click) => super::open(&handle, target.as_ref()),
                Ok(_) => {}
                Err(error) => log::warn!("could not show a notification: {error}"),
            }
        });
    }
}

#[cfg(target_os = "linux")]
mod platform {
    use notify_rust::Notification;
    use tauri::AppHandle;
    use url::Url;

    const DEFAULT_ACTION: &str = "default";

    /// Uses the desktop's notification service over D-Bus; waiting for the action blocks, so it runs on a blocking
    /// thread.
    pub fn show(app: &AppHandle, title: &str, body: &str, target: Option<Url>) {
        let (handle, title, body) = (app.clone(), title.to_string(), body.to_string());
        tauri::async_runtime::spawn_blocking(move || {
            let shown = Notification::new()
                .appname("Sentium")
                .summary(&title)
                .body(&body)
                .action(DEFAULT_ACTION, "Open")
                .show();

            match shown {
                Ok(notification) => notification.wait_for_action(|action| {
                    if action == DEFAULT_ACTION {
                        super::open(&handle, target.as_ref());
                    }
                }),
                Err(error) => log::warn!("could not show a notification: {error}"),
            }
        });
    }
}
