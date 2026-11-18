//! The Device endpoints of a Sentium server. The wire types are copies of the server's records (the app references
//! nothing from the server), so a rename on either side is a breaking change for paired Devices.
//!
//! TLS goes through the operating system's own stack and trust store, and there is deliberately no way to skip
//! certificate validation.

use std::time::Duration;

use futures_util::StreamExt;
use reqwest::{Client, Method, RequestBuilder, Response, StatusCode};
use serde::{Deserialize, Serialize};
use url::Url;

use crate::store::Paired;

const REQUEST_TIMEOUT: Duration = Duration::from_secs(30);

/// A vault batch can carry several megabytes of notes and parses them on the server.
const VAULT_REQUEST_TIMEOUT: Duration = Duration::from_secs(120);
const VAULT_PATH: &str = "api/memoria/device/vault";

/// The server sends a heartbeat comment every 20 seconds; this much silence means the connection is dead (for
/// example after the PC woke from sleep).
const STREAM_IDLE_TIMEOUT: Duration = Duration::from_secs(60);

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DeviceReport {
    pub machine_name: String,
    pub os_description: String,
    pub uptime_seconds: u64,
    pub app_version: String,
    pub cpu_percent: f64,
    pub memory_total_bytes: u64,
    pub memory_available_bytes: u64,
    pub disks: Vec<DiskReport>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DiskReport {
    pub name: String,
    pub total_bytes: u64,
    pub available_bytes: u64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ReportResponse {
    next_report_seconds: u64,
}

#[derive(Serialize)]
struct PairRequest<'a> {
    code: &'a str,
    name: &'a str,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PairResponse {
    pub device_id: String,
    pub token: String,
}

#[derive(Deserialize)]
struct UnreadCount {
    count: u32,
}

#[derive(Deserialize)]
struct Problem {
    detail: Option<String>,
}

/// One frame of the device notification stream: `connected`, `notification` (carrying the Notification) or `read`.
#[derive(Deserialize)]
pub struct Frame {
    #[serde(rename = "type")]
    pub kind: String,
    pub data: Option<serde_json::Value>,
}

/// The fields of a server Notification the app uses.
#[derive(Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Notification {
    pub title: String,
    pub body: Option<String>,
    pub link: Option<String>,
    pub read_at: Option<String>,
}

/// The user's vault as this Device sees it.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DeviceVault {
    pub is_this_device: bool,
    pub note_count: u32,
}

#[derive(Serialize)]
struct ClaimVault<'a> {
    name: &'a str,
    takeover: bool,
}

#[derive(Serialize)]
pub struct ManifestEntry {
    pub path: String,
    pub hash: String,
}

#[derive(Serialize)]
struct VaultManifest<'a> {
    notes: &'a [ManifestEntry],
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ManifestResult {
    /// Paths the server lacks or holds in another version.
    pub upload: Vec<String>,
    pub note_count: u32,
}

#[derive(Serialize)]
pub struct NoteUpload {
    pub path: String,
    pub hash: String,
    pub content: String,
}

#[derive(Serialize)]
struct UploadNotes<'a> {
    notes: &'a [NoteUpload],
}

#[derive(Serialize)]
struct RemoveNotes<'a> {
    paths: &'a [String],
}

#[derive(Serialize)]
struct MoveNote<'a> {
    from: &'a str,
    to: &'a str,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VaultSyncResult {
    pub note_count: u32,
}

#[derive(Debug)]
pub enum ApiError {
    /// 401: the Device was removed (in the portal, by a Sovereign, or with its owner) and its token is dead.
    Unpaired,
    /// The operating system does not trust the server's certificate.
    Untrusted,
    /// 400, or a refused pairing code; the text is the server's explanation.
    Rejected(String),
    /// 409: the request conflicts with the server's state (another Device holds the vault).
    Conflict(String),
    /// 404: the thing the request names does not exist (the vault was forgotten in the portal).
    NotFound,
    /// Anything else: unreachable, timed out, a 5xx. Retried with backoff.
    Failed(String),
}

impl std::fmt::Display for ApiError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ApiError::Unpaired => write!(f, "This device is no longer paired."),
            ApiError::Untrusted => write!(
                f,
                "This PC does not trust the server's certificate. Install the Sentium root certificate (see the installation guide) and try again."
            ),
            ApiError::Rejected(message)
            | ApiError::Conflict(message)
            | ApiError::Failed(message) => {
                write!(f, "{message}")
            }
            ApiError::NotFound => write!(f, "The server does not know this item."),
        }
    }
}

impl From<reqwest::Error> for ApiError {
    fn from(error: reqwest::Error) -> Self {
        if is_certificate_error(&error) {
            ApiError::Untrusted
        } else {
            ApiError::Failed(error.to_string())
        }
    }
}

/// TLS stacks report trust failures as text (Schannel: "...certificate chain ... not trusted", Security.framework:
/// "...certificate was not trusted", OpenSSL: "certificate verify failed"), so the error chain is searched for it.
fn is_certificate_error(error: &(dyn std::error::Error + 'static)) -> bool {
    let mut current: Option<&(dyn std::error::Error + 'static)> = Some(error);
    while let Some(e) = current {
        if e.to_string().to_ascii_lowercase().contains("certificate") {
            return true;
        }
        current = e.source();
    }
    false
}

#[derive(Clone)]
pub struct Api {
    http: Client,
}

impl Api {
    pub fn new(app_version: &str) -> Self {
        let http = Client::builder()
            .user_agent(format!("SentiumSatelles/{app_version}"))
            .connect_timeout(Duration::from_secs(15))
            .pool_idle_timeout(Duration::from_secs(90))
            .build()
            .expect("the HTTP client configuration is static");

        Self { http }
    }

    pub async fn pair(
        &self,
        server: &Url,
        code: &str,
        name: &str,
    ) -> Result<PairResponse, ApiError> {
        let response = self
            .http
            .post(join(server, "api/identity/devices/pair"))
            .timeout(REQUEST_TIMEOUT)
            .json(&PairRequest { code, name })
            .send()
            .await?;

        if !response.status().is_success() {
            return Err(ApiError::Rejected(rejection_message(response).await));
        }

        Ok(response.json().await?)
    }

    pub async fn unpair(&self, paired: &Paired) -> Result<(), ApiError> {
        self.send(self.request(paired, Method::DELETE, "api/identity/devices/current"))
            .await?;
        Ok(())
    }

    /// Returns how long to wait before the next Report.
    pub async fn report(
        &self,
        paired: &Paired,
        report: &DeviceReport,
    ) -> Result<Duration, ApiError> {
        let response = self
            .send(
                self.request(paired, Method::POST, "api/vigil/devices/report")
                    .json(report),
            )
            .await?;
        let body: ReportResponse = response.json().await?;
        Ok(Duration::from_secs(body.next_report_seconds))
    }

    pub async fn unread_count(&self, paired: &Paired) -> Result<u32, ApiError> {
        let response = self
            .send(self.request(paired, Method::GET, "api/nuntius/devices/unread-count"))
            .await?;
        Ok(response.json::<UnreadCount>().await?.count)
    }

    /// Opens the notification stream and hands every frame to `on_frame` until the server closes it or the
    /// connection goes quiet.
    pub async fn read_notifications<F, Fut>(
        &self,
        paired: &Paired,
        mut on_frame: F,
    ) -> Result<(), ApiError>
    where
        F: FnMut(Frame) -> Fut,
        Fut: std::future::Future<Output = Result<(), ApiError>>,
    {
        let request = self
            .http
            .request(
                Method::GET,
                join(&paired.server, "api/nuntius/devices/stream"),
            )
            .bearer_auth(&paired.token);
        let response = tokio::time::timeout(REQUEST_TIMEOUT, self.send(request))
            .await
            .map_err(|_| ApiError::Failed("The notification stream did not open.".into()))??;

        let mut body = response.bytes_stream();
        let mut pending = Vec::new();

        loop {
            let chunk = match tokio::time::timeout(STREAM_IDLE_TIMEOUT, body.next()).await {
                Err(_) => {
                    return Err(ApiError::Failed(
                        "The notification stream went quiet.".into(),
                    ));
                }
                Ok(None) => return Ok(()),
                Ok(Some(chunk)) => chunk?,
            };

            pending.extend_from_slice(&chunk);
            while let Some(end) = pending.iter().position(|&b| b == b'\n') {
                let line: Vec<u8> = pending.drain(..=end).collect();
                let line = String::from_utf8_lossy(&line);
                if let Some(data) = line.trim_end().strip_prefix("data:")
                    && let Ok(frame) = serde_json::from_str::<Frame>(data.trim())
                {
                    on_frame(frame).await?;
                }
            }
        }
    }

    /// Claims the user's vault for this Device; `takeover` moves it here from another paired Device.
    pub async fn claim_vault(
        &self,
        paired: &Paired,
        name: &str,
        takeover: bool,
    ) -> Result<DeviceVault, ApiError> {
        let response = self
            .send(
                self.vault_request(paired, Method::PUT, VAULT_PATH)
                    .json(&ClaimVault { name, takeover }),
            )
            .await?;
        Ok(response.json().await?)
    }

    /// Sends every note's path and hash; the server drops notes not listed and answers which ones to upload.
    pub async fn vault_manifest(
        &self,
        paired: &Paired,
        notes: &[ManifestEntry],
    ) -> Result<ManifestResult, ApiError> {
        let response = self
            .send(
                self.vault_request(paired, Method::POST, &format!("{VAULT_PATH}/manifest"))
                    .json(&VaultManifest { notes }),
            )
            .await?;
        Ok(response.json().await?)
    }

    pub async fn upload_notes(
        &self,
        paired: &Paired,
        notes: &[NoteUpload],
    ) -> Result<VaultSyncResult, ApiError> {
        let response = self
            .send(
                self.vault_request(paired, Method::PUT, &format!("{VAULT_PATH}/notes"))
                    .json(&UploadNotes { notes }),
            )
            .await?;
        Ok(response.json().await?)
    }

    pub async fn remove_notes(
        &self,
        paired: &Paired,
        paths: &[String],
    ) -> Result<VaultSyncResult, ApiError> {
        let response = self
            .send(
                self.vault_request(paired, Method::POST, &format!("{VAULT_PATH}/notes/remove"))
                    .json(&RemoveNotes { paths }),
            )
            .await?;
        Ok(response.json().await?)
    }

    /// Renames a note on the server, keeping its links; `NotFound` when the server never had it.
    pub async fn move_note(
        &self,
        paired: &Paired,
        from: &str,
        to: &str,
    ) -> Result<VaultSyncResult, ApiError> {
        let response = self
            .send(
                self.vault_request(paired, Method::POST, &format!("{VAULT_PATH}/notes/move"))
                    .json(&MoveNote { from, to }),
            )
            .await?;
        Ok(response.json().await?)
    }

    fn vault_request(&self, paired: &Paired, method: Method, path: &str) -> RequestBuilder {
        self.request(paired, method, path)
            .timeout(VAULT_REQUEST_TIMEOUT)
    }

    fn request(&self, paired: &Paired, method: Method, path: &str) -> RequestBuilder {
        self.http
            .request(method, join(&paired.server, path))
            .timeout(REQUEST_TIMEOUT)
            .bearer_auth(&paired.token)
    }

    async fn send(&self, request: RequestBuilder) -> Result<Response, ApiError> {
        let response = request.send().await?;
        match response.status() {
            StatusCode::UNAUTHORIZED => Err(ApiError::Unpaired),
            StatusCode::NOT_FOUND => Err(ApiError::NotFound),
            StatusCode::CONFLICT => Err(ApiError::Conflict(problem_detail(response).await)),
            StatusCode::BAD_REQUEST => Err(ApiError::Rejected(problem_detail(response).await)),
            status if !status.is_success() => {
                Err(ApiError::Failed(format!("The server answered {status}.")))
            }
            _ => Ok(response),
        }
    }
}

async fn problem_detail(response: Response) -> String {
    let status = response.status();
    match response.json::<Problem>().await {
        Ok(Problem {
            detail: Some(detail),
        }) if !detail.is_empty() => detail,
        _ => format!("The server answered {status}."),
    }
}

fn join(server: &Url, path: &str) -> Url {
    server
        .join(path)
        .expect("API paths are static and relative")
}

async fn rejection_message(response: Response) -> String {
    let status = response.status();
    if status == StatusCode::TOO_MANY_REQUESTS {
        return "Too many attempts. Wait a minute and try again.".into();
    }

    match response.json::<Problem>().await {
        Ok(Problem {
            detail: Some(detail),
        }) if !detail.is_empty() => detail,
        _ => format!("The server refused the pairing ({}).", status.as_u16()),
    }
}
