//! Where the pairing survives restarts, for the current OS user only. The server address and device id live in a
//! JSON file in the app's local data folder; the token lives in the platform credential store (Windows Credential
//! Manager, macOS Keychain, the Secret Service on Linux), which only that user can read.

use std::fs;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};
use url::Url;

const KEYRING_SERVICE: &str = "dev.sentium.satelles";
const KEYRING_ACCOUNT: &str = "device-token";
const SETTINGS_FILE: &str = "satelles.json";

#[derive(Clone)]
pub struct Paired {
    pub server: Url,
    pub device_id: String,
    pub token: String,
}

#[derive(Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
struct Settings {
    server_url: Option<Url>,
    device_id: Option<String>,
}

pub struct Store {
    folder: PathBuf,
}

impl Store {
    pub fn new(folder: PathBuf) -> Self {
        Self { folder }
    }

    /// The stored pairing, or `None` when there is none or the token cannot be read.
    pub fn load(&self) -> Option<Paired> {
        let settings = self.read_settings();
        let (server, device_id) = (settings.server_url?, settings.device_id?);

        match token_entry().and_then(|entry| entry.get_password()) {
            Ok(token) => Some(Paired {
                server,
                device_id,
                token,
            }),
            Err(keyring::Error::NoEntry) => None,
            Err(error) => {
                log::warn!("the stored device token could not be read: {error}");
                None
            }
        }
    }

    /// The last server this PC was paired with, kept after unpairing to prefill the pairing window.
    pub fn last_server(&self) -> Option<Url> {
        self.read_settings().server_url
    }

    pub fn save(&self, paired: &Paired) -> Result<(), String> {
        token_entry()
            .and_then(|entry| entry.set_password(&paired.token))
            .map_err(|e| format!("Could not store the device credential: {e}"))?;

        self.write_settings(&Settings {
            server_url: Some(paired.server.clone()),
            device_id: Some(paired.device_id.clone()),
        })
    }

    /// Forgets the credential, keeping the server address.
    pub fn clear(&self) {
        match token_entry().and_then(|entry| entry.delete_credential()) {
            Ok(()) | Err(keyring::Error::NoEntry) => {}
            Err(error) => log::warn!("could not delete the device token: {error}"),
        }

        let settings = Settings {
            device_id: None,
            ..self.read_settings()
        };
        if let Err(error) = self.write_settings(&settings) {
            log::warn!("{error}");
        }
    }

    fn read_settings(&self) -> Settings {
        fs::read(self.folder.join(SETTINGS_FILE))
            .ok()
            .and_then(|bytes| serde_json::from_slice(&bytes).ok())
            .unwrap_or_default()
    }

    fn write_settings(&self, settings: &Settings) -> Result<(), String> {
        let write = || -> std::io::Result<()> {
            fs::create_dir_all(&self.folder)?;
            let path = self.folder.join(SETTINGS_FILE);
            let temp = path.with_extension("json.tmp");
            fs::write(&temp, serde_json::to_vec_pretty(settings)?)?;
            fs::rename(temp, path)
        };

        write().map_err(|e| format!("Could not save the app settings: {e}"))
    }
}

fn token_entry() -> keyring::Result<keyring::Entry> {
    keyring::Entry::new(KEYRING_SERVICE, KEYRING_ACCOUNT)
}
