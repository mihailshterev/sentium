//! The connection to the paired server: a Report loop and a notification stream loop, each retrying with backoff.
//! A 401 on either means the Device was removed, so both stop and the app returns to "not paired". The server can
//! only ever make this app show a toast; nothing it sends is executed.

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use serde::Serialize;
use tokio_util::sync::CancellationToken;
use url::Url;

use crate::api::{Api, ApiError, Frame, Notification};
use crate::metrics::Metrics;
use crate::store::{Paired, Store};

const DEFAULT_REPORT_INTERVAL: Duration = Duration::from_secs(30);
const MIN_REPORT_INTERVAL: Duration = Duration::from_secs(10);
const MAX_REPORT_INTERVAL: Duration = Duration::from_secs(3_600);

#[derive(Clone, Copy, PartialEq, Eq, Serialize)]
pub enum Status {
    NotPaired,
    Connecting,
    Connected,
    /// The server cannot be reached; the loops keep retrying.
    Offline,
    /// The operating system does not trust the server's certificate. There is no override.
    Untrusted,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Snapshot {
    pub status: Status,
    pub status_text: String,
    pub paired: bool,
    pub server: Option<String>,
    pub last_server: Option<String>,
    pub unread: u32,
}

/// Receives the agent's changes; called from background tasks.
pub trait Observer: Send + Sync + 'static {
    fn state_changed(&self, snapshot: Snapshot);
    fn notification(&self, server: &Url, notification: Notification);
}

struct Inner {
    status: Status,
    paired: Option<Paired>,
    unread: u32,
    /// Bumped whenever the loops are (re)started or stopped, so a late result from an old loop is ignored.
    generation: u64,
    cancel: Option<CancellationToken>,
}

pub struct Agent {
    api: Api,
    store: Store,
    metrics: Mutex<Metrics>,
    inner: Mutex<Inner>,
    observer: Arc<dyn Observer>,
    app_version: String,
}

impl Agent {
    pub fn new(
        api: Api,
        store: Store,
        app_version: String,
        observer: Arc<dyn Observer>,
    ) -> Arc<Self> {
        Arc::new(Self {
            api,
            store,
            metrics: Mutex::new(Metrics::new()),
            inner: Mutex::new(Inner {
                status: Status::NotPaired,
                paired: None,
                unread: 0,
                generation: 0,
                cancel: None,
            }),
            observer,
            app_version,
        })
    }

    /// Resumes the stored pairing, if any.
    pub fn start(self: &Arc<Self>) {
        match self.store.load() {
            Some(paired) => self.start_loops(paired),
            None => self.publish(),
        }
    }

    pub fn is_paired(&self) -> bool {
        self.lock().paired.is_some()
    }

    pub fn server(&self) -> Option<Url> {
        self.lock().paired.as_ref().map(|p| p.server.clone())
    }

    pub fn snapshot(&self) -> Snapshot {
        let inner = self.lock();
        let server = inner.paired.as_ref().map(|p| p.server.clone());
        let host = server.as_ref().map(authority).unwrap_or_default();

        let status_text = match inner.status {
            Status::NotPaired => "Not paired".to_string(),
            Status::Connecting => format!("Connecting to {host}…"),
            Status::Connected => format!("Connected to {host}"),
            Status::Offline => format!("Cannot reach {host}, retrying"),
            Status::Untrusted => format!("Certificate of {host} is not trusted"),
        };

        Snapshot {
            status: inner.status,
            status_text,
            paired: server.is_some(),
            last_server: server
                .clone()
                .or_else(|| self.store.last_server())
                .map(|u| u.as_str().trim_end_matches('/').to_string()),
            server: server.map(|u| u.to_string()),
            unread: inner.unread,
        }
    }

    pub async fn pair(self: &Arc<Self>, server: Url, code: &str, name: &str) -> Result<(), String> {
        let response = self
            .api
            .pair(&server, code, name)
            .await
            .map_err(|e| e.to_string())?;
        let paired = Paired {
            server,
            device_id: response.device_id,
            token: response.token,
        };

        self.store.save(&paired)?;
        self.start_loops(paired);
        Ok(())
    }

    /// Removes this Device on the server and forgets the credential. The credential is forgotten even when the
    /// server cannot be reached; returns `false` then, so the person can remove it in the portal.
    pub async fn unpair(self: &Arc<Self>) -> bool {
        let paired = self.stop_loops();

        let removed_on_server = match &paired {
            Some(paired) => matches!(
                self.api.unpair(paired).await,
                Ok(()) | Err(ApiError::Unpaired)
            ),
            None => true,
        };

        self.forget();
        removed_on_server
    }

    pub fn shutdown(&self) {
        self.stop_loops();
    }

    fn start_loops(self: &Arc<Self>, paired: Paired) {
        let cancel = CancellationToken::new();
        let generation = {
            let mut inner = self.lock();
            if let Some(previous) = inner.cancel.replace(cancel.clone()) {
                previous.cancel();
            }
            inner.generation += 1;
            inner.paired = Some(paired.clone());
            inner.unread = 0;
            inner.status = Status::Connecting;
            inner.generation
        };
        self.publish();

        tauri::async_runtime::spawn(Arc::clone(self).report_loop(
            paired.clone(),
            generation,
            cancel.clone(),
        ));
        tauri::async_runtime::spawn(Arc::clone(self).stream_loop(paired, generation, cancel));
    }

    fn stop_loops(&self) -> Option<Paired> {
        let mut inner = self.lock();
        inner.generation += 1;
        if let Some(cancel) = inner.cancel.take() {
            cancel.cancel();
        }
        inner.paired.clone()
    }

    async fn report_loop(
        self: Arc<Self>,
        paired: Paired,
        generation: u64,
        cancel: CancellationToken,
    ) {
        let mut backoff = Backoff::default();

        loop {
            let report = self
                .metrics
                .lock()
                .expect("metrics lock")
                .collect(&self.app_version);

            let result = tokio::select! {
                _ = cancel.cancelled() => return,
                result = self.api.report(&paired, &report) => result,
            };

            let delay = match result {
                Ok(next) => {
                    backoff.reset();
                    self.set_status(generation, Status::Connected);
                    next.clamp(MIN_REPORT_INTERVAL, MAX_REPORT_INTERVAL)
                }
                Err(ApiError::Unpaired) => {
                    self.removed_by_server(generation);
                    return;
                }
                Err(error) => {
                    log::debug!("report failed: {error}");
                    self.set_status(generation, status_for(&error));
                    backoff.next().min(DEFAULT_REPORT_INTERVAL)
                }
            };

            tokio::select! {
                _ = cancel.cancelled() => return,
                _ = tokio::time::sleep(delay) => {}
            }
        }
    }

    async fn stream_loop(
        self: Arc<Self>,
        paired: Paired,
        generation: u64,
        cancel: CancellationToken,
    ) {
        let mut backoff = Backoff::default();

        loop {
            let connected = Arc::new(AtomicBool::new(false));
            let read = self.api.read_notifications(&paired, |frame| {
                let this = Arc::clone(&self);
                let paired = paired.clone();
                let connected = Arc::clone(&connected);
                async move {
                    this.handle_frame(&paired, generation, frame, &connected)
                        .await
                }
            });

            let result = tokio::select! {
                _ = cancel.cancelled() => return,
                result = read => result,
            };

            match result {
                Err(ApiError::Unpaired) => {
                    self.removed_by_server(generation);
                    return;
                }
                Err(error) => {
                    log::debug!("notification stream dropped: {error}");
                    self.set_status(generation, status_for(&error));
                }
                Ok(()) => {}
            }

            if connected.load(Ordering::Relaxed) {
                backoff.reset();
            }

            tokio::select! {
                _ = cancel.cancelled() => return,
                _ = tokio::time::sleep(backoff.next()) => {}
            }
        }
    }

    async fn handle_frame(
        &self,
        paired: &Paired,
        generation: u64,
        frame: Frame,
        connected: &AtomicBool,
    ) -> Result<(), ApiError> {
        match frame.kind.as_str() {
            // Missed toasts are not replayed after a reconnect; the unread count stands in for them.
            "connected" => {
                connected.store(true, Ordering::Relaxed);
                self.set_status(generation, Status::Connected);
                self.refresh_unread(paired, generation).await
            }
            "notification" => {
                let notification = frame
                    .data
                    .and_then(|data| serde_json::from_value::<Notification>(data).ok());
                if let Some(notification) = notification.filter(|n| n.read_at.is_none())
                    && self.is_current(generation)
                {
                    self.observer.notification(&paired.server, notification);
                }
                self.refresh_unread(paired, generation).await
            }
            "read" => self.refresh_unread(paired, generation).await,
            _ => Ok(()),
        }
    }

    async fn refresh_unread(&self, paired: &Paired, generation: u64) -> Result<(), ApiError> {
        let count = self.api.unread_count(paired).await?;
        {
            let mut inner = self.lock();
            if inner.generation != generation || inner.unread == count {
                return Ok(());
            }
            inner.unread = count;
        }
        self.publish();
        Ok(())
    }

    fn removed_by_server(&self, generation: u64) {
        {
            let mut inner = self.lock();
            if inner.generation != generation {
                return;
            }
            inner.generation += 1;
            if let Some(cancel) = inner.cancel.take() {
                cancel.cancel();
            }
        }

        log::info!("the server no longer accepts this device; it was removed");
        self.forget();
    }

    fn forget(&self) {
        self.store.clear();
        {
            let mut inner = self.lock();
            inner.paired = None;
            inner.unread = 0;
            inner.status = Status::NotPaired;
        }
        self.publish();
    }

    fn set_status(&self, generation: u64, status: Status) {
        {
            let mut inner = self.lock();
            if inner.generation != generation || inner.status == status {
                return;
            }
            inner.status = status;
        }
        self.publish();
    }

    fn is_current(&self, generation: u64) -> bool {
        self.lock().generation == generation
    }

    fn publish(&self) {
        self.observer.state_changed(self.snapshot());
    }

    fn lock(&self) -> std::sync::MutexGuard<'_, Inner> {
        self.inner.lock().expect("agent state lock")
    }
}

fn status_for(error: &ApiError) -> Status {
    match error {
        ApiError::Untrusted => Status::Untrusted,
        _ => Status::Offline,
    }
}

fn authority(url: &Url) -> String {
    match (url.host_str(), url.port()) {
        (Some(host), Some(port)) => format!("{host}:{port}"),
        (Some(host), None) => host.to_string(),
        _ => url.to_string(),
    }
}

/// Capped exponential backoff with jitter, so a household of PCs waking together does not hit a restarting server
/// in lockstep.
#[derive(Default)]
struct Backoff {
    attempt: u32,
}

impl Backoff {
    const INITIAL: Duration = Duration::from_secs(2);
    const CAP: Duration = Duration::from_secs(120);

    fn next(&mut self) -> Duration {
        let exponential = Self::INITIAL.saturating_mul(1 << self.attempt.min(10));
        self.attempt = self.attempt.saturating_add(1);
        exponential
            .min(Self::CAP)
            .mul_f64(0.5 + fastrand::f64() / 2.0)
    }

    fn reset(&mut self) {
        self.attempt = 0;
    }
}
