//! One-way sync of the chosen Obsidian vault to the server's Memoria module. Sentium only reads the vault: nothing
//! the server sends can write to, rename or delete a file on this PC.
//!
//! A sync session claims the vault for this Device, reconciles the whole vault against the server (a manifest of
//! paths and hashes; the server answers which notes to upload and drops the ones that are gone), then follows a
//! debounced file watcher and re-reconciles every 15 minutes to catch anything the watcher missed (sleep, offline
//! edits, a sync tool rewriting the folder).

use std::collections::BTreeSet;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use notify_debouncer_full::notify::event::{ModifyKind, RemoveKind, RenameMode};
use notify_debouncer_full::notify::{EventKind, RecursiveMode};
use notify_debouncer_full::{DebounceEventResult, DebouncedEvent, new_debouncer};
use serde::Serialize;
use tokio::sync::{Notify, mpsc};
use tokio::time::Instant;
use tokio_util::sync::CancellationToken;

use crate::agent::Backoff;
use crate::api::{Api, ApiError, ManifestEntry, NoteUpload};
use crate::store::{Paired, Store, VaultSettings};
use crate::vault_files::{self, NoteFile, PathFilter};

const DEBOUNCE: Duration = Duration::from_secs(2);
const RECONCILE_INTERVAL: Duration = Duration::from_secs(15 * 60);
/// Matches the server's per-request limit.
const NOTES_PER_BATCH: usize = 50;
/// Well under the server's request size limit, even with JSON escaping.
const BYTES_PER_BATCH: usize = 8 * 1024 * 1024;
const PATHS_PER_REMOVE: usize = 500;
const MAX_NOTES: usize = 20_000;

#[derive(Clone, Copy, PartialEq, Eq, Serialize, Debug)]
pub enum VaultState {
    /// No vault chosen, or this PC is not paired.
    Off,
    Paused,
    Connecting,
    Syncing,
    Synced,
    /// Another paired Device syncs the user's vault; this one waits until the person takes it over.
    HeldElsewhere,
    Error,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VaultSnapshot {
    pub folder: Option<String>,
    pub name: Option<String>,
    pub excludes: Vec<String>,
    pub state: VaultState,
    pub status_text: String,
    pub note_count: Option<u32>,
    /// Unix milliseconds of the last completed sync, so the window can say "synced 2 min ago".
    pub last_sync_ms: Option<u64>,
}

struct Inner {
    state: VaultState,
    message: Option<String>,
    note_count: Option<u32>,
    last_sync_ms: Option<u64>,
    paired: Option<Paired>,
    /// Bumped whenever the session is (re)started or stopped, so a late result from an old session is ignored.
    generation: u64,
    cancel: Option<CancellationToken>,
    takeover: bool,
}

enum Exit {
    Stop,
    Reclaim,
}

pub struct VaultSync {
    api: Api,
    store: Store,
    on_change: Box<dyn Fn() + Send + Sync>,
    sync_now: Notify,
    inner: Mutex<Inner>,
}

impl VaultSync {
    /// `on_change` is called after every state change, never while a lock is held.
    pub fn new(api: Api, store: Store, on_change: Box<dyn Fn() + Send + Sync>) -> Arc<Self> {
        Arc::new(Self {
            api,
            store,
            on_change,
            sync_now: Notify::new(),
            inner: Mutex::new(Inner {
                state: VaultState::Off,
                message: None,
                note_count: None,
                last_sync_ms: None,
                paired: None,
                generation: 0,
                cancel: None,
                takeover: false,
            }),
        })
    }

    pub fn settings(&self) -> Option<VaultSettings> {
        self.store.vault()
    }

    pub fn start(self: &Arc<Self>, paired: Paired) {
        self.lock().paired = Some(paired);
        self.restart();
    }

    pub fn stop(&self) {
        {
            let mut inner = self.lock();
            inner.paired = None;
            Self::cancel(&mut inner);
            inner.state = VaultState::Off;
            inner.note_count = None;
        }
        (self.on_change)();
    }

    /// Saves the vault choice (`None` stops syncing) and restarts the session with it.
    pub fn configure(self: &Arc<Self>, vault: Option<VaultSettings>) -> Result<(), String> {
        if let Some(vault) = &vault {
            PathFilter::new(&vault.excludes)?;
        }

        let changed_folder =
            self.store.vault().map(|v| v.path) != vault.as_ref().map(|v| v.path.clone());
        self.store.save_vault(vault)?;

        {
            let mut inner = self.lock();
            inner.takeover = false;
            if changed_folder {
                inner.note_count = None;
                inner.last_sync_ms = None;
            }
        }

        self.restart();
        Ok(())
    }

    pub fn take_over(self: &Arc<Self>) {
        self.lock().takeover = true;
        self.restart();
    }

    pub fn sync_now(&self) {
        self.sync_now.notify_one();
    }

    pub fn snapshot(&self) -> VaultSnapshot {
        let settings = self.store.vault();
        let inner = self.lock();

        let status_text = match inner.state {
            VaultState::Off => "Not syncing a vault.".to_string(),
            VaultState::Paused => "Paused.".to_string(),
            VaultState::Connecting => "Connecting…".to_string(),
            VaultState::Syncing => "Syncing…".to_string(),
            VaultState::Synced => match inner.note_count {
                Some(count) => format!("Up to date · {count} notes"),
                None => "Up to date".to_string(),
            },
            VaultState::HeldElsewhere => {
                "Another of your devices syncs this vault. Take it over to sync from this PC."
                    .to_string()
            }
            VaultState::Error => inner
                .message
                .clone()
                .unwrap_or_else(|| "Sync failed; retrying.".to_string()),
        };

        VaultSnapshot {
            folder: settings.as_ref().map(|v| v.path.clone()),
            name: settings.as_ref().map(|v| v.name.clone()),
            excludes: settings.map(|v| v.excludes).unwrap_or_default(),
            state: inner.state,
            status_text,
            note_count: inner.note_count,
            last_sync_ms: inner.last_sync_ms,
        }
    }

    fn restart(self: &Arc<Self>) {
        let settings = self.store.vault();
        let session = {
            let mut inner = self.lock();
            Self::cancel(&mut inner);

            match (inner.paired.clone(), settings) {
                (Some(paired), Some(vault)) if !vault.paused => {
                    let cancel = CancellationToken::new();
                    inner.cancel = Some(cancel.clone());
                    inner.state = VaultState::Connecting;
                    inner.message = None;
                    Some((paired, vault, inner.takeover, inner.generation, cancel))
                }
                (_, Some(vault)) if vault.paused => {
                    inner.state = VaultState::Paused;
                    None
                }
                _ => {
                    inner.state = VaultState::Off;
                    None
                }
            }
        };
        (self.on_change)();

        if let Some((paired, vault, takeover, generation, cancel)) = session {
            tauri::async_runtime::spawn(
                Arc::clone(self).run(paired, vault, takeover, generation, cancel),
            );
        }
    }

    async fn run(
        self: Arc<Self>,
        paired: Paired,
        vault: VaultSettings,
        mut takeover: bool,
        generation: u64,
        cancel: CancellationToken,
    ) {
        let root = PathBuf::from(&vault.path);
        let filter = match PathFilter::new(&vault.excludes) {
            Ok(filter) => Arc::new(filter),
            Err(error) => {
                self.set(generation, VaultState::Error, Some(error));
                return;
            }
        };

        let mut backoff = Backoff::default();

        loop {
            if !root.is_dir() {
                self.set(
                    generation,
                    VaultState::Error,
                    Some(format!(
                        "The folder {} is not available; retrying.",
                        root.display()
                    )),
                );
            } else {
                match self.api.claim_vault(&paired, &vault.name, takeover).await {
                    Ok(claimed) if claimed.is_this_device => {
                        takeover = false;
                        self.lock().takeover = false;
                        backoff.reset();
                        self.record_count(generation, claimed.note_count);

                        match self
                            .session(&paired, &root, &filter, generation, &cancel)
                            .await
                        {
                            Exit::Stop => return,
                            Exit::Reclaim => continue,
                        }
                    }
                    Ok(_) | Err(ApiError::Conflict(_)) => {
                        self.set(generation, VaultState::HeldElsewhere, None);
                        return;
                    }
                    // The agent's own loops notice the dead token and unpair; this session just ends.
                    Err(ApiError::Unpaired) => return,
                    Err(error) => self.set(generation, VaultState::Error, Some(error.to_string())),
                }
            }

            tokio::select! {
                _ = cancel.cancelled() => return,
                _ = tokio::time::sleep(backoff.next()) => {}
            }
        }
    }

    async fn session(
        &self,
        paired: &Paired,
        root: &Path,
        filter: &Arc<PathFilter>,
        generation: u64,
        cancel: &CancellationToken,
    ) -> Exit {
        let (sender, mut events) = mpsc::unbounded_channel::<DebounceEventResult>();
        let watcher = new_debouncer(DEBOUNCE, None, move |result: DebounceEventResult| {
            let _ = sender.send(result);
        })
        .and_then(|mut debouncer| {
            debouncer
                .watch(root, RecursiveMode::Recursive)
                .map(|()| debouncer)
        });

        // Without a watcher the periodic reconcile still keeps the server current, only later.
        let _watcher = watcher
            .inspect_err(|error| log::warn!("the vault watcher could not start: {error}"))
            .ok();

        let mut changes = Changes::default();
        let mut needs_reconcile = true;
        let mut next_reconcile = Instant::now();
        let mut backoff = Backoff::default();

        loop {
            let outcome = if needs_reconcile || Instant::now() >= next_reconcile {
                changes = Changes::default();
                self.reconcile(paired, root, filter, generation).await
            } else if !changes.is_empty() {
                self.apply(paired, root, std::mem::take(&mut changes), generation)
                    .await
            } else {
                Ok(())
            };

            let wake_at = match outcome {
                Ok(()) => {
                    if needs_reconcile || Instant::now() >= next_reconcile {
                        next_reconcile = Instant::now() + RECONCILE_INTERVAL;
                    }
                    needs_reconcile = false;
                    backoff.reset();
                    next_reconcile
                }
                Err(ApiError::Unpaired) => return Exit::Stop,
                Err(ApiError::Conflict(_)) => {
                    self.set(generation, VaultState::HeldElsewhere, None);
                    return Exit::Stop;
                }
                Err(ApiError::NotFound) => return Exit::Reclaim,
                Err(error) => {
                    self.set(generation, VaultState::Error, Some(error.to_string()));
                    needs_reconcile = true;
                    Instant::now() + backoff.next()
                }
            };

            tokio::select! {
                _ = cancel.cancelled() => return Exit::Stop,
                _ = tokio::time::sleep_until(wake_at) => {}
                _ = self.sync_now.notified() => needs_reconcile = true,
                Some(result) = events.recv() => {
                    needs_reconcile |= !changes.record_all(root, filter, result);
                    while let Ok(result) = events.try_recv() {
                        needs_reconcile |= !changes.record_all(root, filter, result);
                    }
                }
            }
        }
    }

    async fn reconcile(
        &self,
        paired: &Paired,
        root: &Path,
        filter: &Arc<PathFilter>,
        generation: u64,
    ) -> Result<(), ApiError> {
        self.set(generation, VaultState::Syncing, None);

        let (scan_root, scan_filter) = (root.to_path_buf(), Arc::clone(filter));
        let notes = tauri::async_runtime::spawn_blocking(move || {
            vault_files::scan(&scan_root, &scan_filter)
        })
        .await
        .map_err(|e| ApiError::Failed(format!("Reading the vault failed: {e}")))?;

        if notes.len() > MAX_NOTES {
            return Err(ApiError::Failed(format!(
                "The vault has {} notes; Sentium syncs at most {MAX_NOTES}. Exclude some folders.",
                notes.len()
            )));
        }

        let manifest: Vec<ManifestEntry> = notes
            .into_iter()
            .map(|n| ManifestEntry {
                path: n.path,
                hash: n.hash,
            })
            .collect();

        let result = self.api.vault_manifest(paired, &manifest).await?;
        self.record_count(generation, result.note_count);
        self.upload(paired, root, result.upload, generation).await?;

        self.synced(generation);
        Ok(())
    }

    async fn apply(
        &self,
        paired: &Paired,
        root: &Path,
        changes: Changes,
        generation: u64,
    ) -> Result<(), ApiError> {
        self.set(generation, VaultState::Syncing, None);

        let mut upserts = changes.upserts;
        for (from, to) in changes.moves {
            match self.api.move_note(paired, &from, &to).await {
                Ok(result) => self.record_count(generation, result.note_count),
                Err(ApiError::NotFound) => {}
                Err(error) => return Err(error),
            }
            // The server skips an unchanged hash, so this only costs a read when the note also changed.
            upserts.insert(to);
        }

        let removes: Vec<String> = changes.removes.into_iter().collect();
        self.remove(paired, &removes, generation).await?;
        self.upload(paired, root, upserts.into_iter().collect(), generation)
            .await?;

        self.synced(generation);
        Ok(())
    }

    /// Uploads the notes at `paths` in batches. Paths that are gone by now are removed instead, and a note the
    /// server refuses is logged and skipped so it cannot block the rest of the vault.
    async fn upload(
        &self,
        paired: &Paired,
        root: &Path,
        paths: Vec<String>,
        generation: u64,
    ) -> Result<(), ApiError> {
        for chunk in paths.chunks(NOTES_PER_BATCH) {
            let (read_root, read_paths) = (root.to_path_buf(), chunk.to_vec());
            let read = tauri::async_runtime::spawn_blocking(move || {
                read_paths
                    .into_iter()
                    .map(|path| {
                        let note = vault_files::read_note(&read_root, &path);
                        let gone = note.is_none() && !read_root.join(&path).exists();
                        (path, note, gone)
                    })
                    .collect::<Vec<_>>()
            })
            .await
            .map_err(|e| ApiError::Failed(format!("Reading notes failed: {e}")))?;

            let gone: Vec<String> = read
                .iter()
                .filter(|(_, _, gone)| *gone)
                .map(|(path, _, _)| path.clone())
                .collect();
            let notes: Vec<NoteFile> = read.into_iter().filter_map(|(_, note, _)| note).collect();

            for batch in split_by_size(notes) {
                self.send_batch(paired, batch, generation).await?;
            }

            self.remove(paired, &gone, generation).await?;
        }

        Ok(())
    }

    async fn send_batch(
        &self,
        paired: &Paired,
        batch: Vec<NoteUpload>,
        generation: u64,
    ) -> Result<(), ApiError> {
        match self.api.upload_notes(paired, &batch).await {
            Ok(result) => {
                self.record_count(generation, result.note_count);
                Ok(())
            }
            Err(ApiError::Rejected(_)) if batch.len() > 1 => {
                for note in batch {
                    match self
                        .api
                        .upload_notes(paired, std::slice::from_ref(&note))
                        .await
                    {
                        Ok(result) => self.record_count(generation, result.note_count),
                        Err(ApiError::Rejected(reason)) => {
                            log::warn!("the server refused {}: {reason}", note.path)
                        }
                        Err(error) => return Err(error),
                    }
                }
                Ok(())
            }
            Err(ApiError::Rejected(reason)) => {
                log::warn!("the server refused {}: {reason}", batch[0].path);
                Ok(())
            }
            Err(error) => Err(error),
        }
    }

    async fn remove(
        &self,
        paired: &Paired,
        paths: &[String],
        generation: u64,
    ) -> Result<(), ApiError> {
        for chunk in paths.chunks(PATHS_PER_REMOVE) {
            let result = self.api.remove_notes(paired, chunk).await?;
            self.record_count(generation, result.note_count);
        }
        Ok(())
    }

    fn set(&self, generation: u64, state: VaultState, message: Option<String>) {
        {
            let mut inner = self.lock();
            if inner.generation != generation || (inner.state == state && inner.message == message)
            {
                return;
            }
            inner.state = state;
            inner.message = message;
        }
        (self.on_change)();
    }

    fn record_count(&self, generation: u64, count: u32) {
        let mut inner = self.lock();
        if inner.generation == generation {
            inner.note_count = Some(count);
        }
    }

    fn synced(&self, generation: u64) {
        {
            let mut inner = self.lock();
            if inner.generation != generation {
                return;
            }
            inner.state = VaultState::Synced;
            inner.message = None;
            inner.last_sync_ms = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .ok()
                .and_then(|d| u64::try_from(d.as_millis()).ok());
        }
        (self.on_change)();
    }

    fn cancel(inner: &mut Inner) {
        inner.generation += 1;
        if let Some(cancel) = inner.cancel.take() {
            cancel.cancel();
        }
    }

    fn lock(&self) -> std::sync::MutexGuard<'_, Inner> {
        self.inner.lock().expect("vault state lock")
    }
}

/// Note changes seen by the watcher since the last sync, merged so only the final state of each path is sent.
#[derive(Default)]
struct Changes {
    upserts: BTreeSet<String>,
    removes: BTreeSet<String>,
    moves: Vec<(String, String)>,
}

impl Changes {
    fn is_empty(&self) -> bool {
        self.upserts.is_empty() && self.removes.is_empty() && self.moves.is_empty()
    }

    /// Records a watcher batch; `false` when it cannot be applied note by note and needs a full reconcile.
    fn record_all(
        &mut self,
        root: &Path,
        filter: &PathFilter,
        result: DebounceEventResult,
    ) -> bool {
        match result {
            Ok(events) => {
                // Every event is recorded even after one asks for a reconcile.
                let mut applicable = true;
                for event in &events {
                    applicable &= self.record(root, filter, event);
                }
                applicable
            }
            Err(errors) => {
                for error in errors {
                    log::debug!("vault watcher error: {error}");
                }
                false
            }
        }
    }

    /// `false` for a change to a folder (created, moved or deleted): its notes are not listed one by one.
    fn record(&mut self, root: &Path, filter: &PathFilter, event: &DebouncedEvent) -> bool {
        let note =
            |path: &Path| vault_files::relative_path(root, path).filter(|p| filter.is_note(p));

        match event.kind {
            EventKind::Access(_) => true,
            EventKind::Modify(ModifyKind::Name(RenameMode::Both)) if event.paths.len() == 2 => {
                if event.paths[1].is_dir() {
                    return false;
                }

                match (note(&event.paths[0]), note(&event.paths[1])) {
                    (Some(from), Some(to)) => {
                        self.upserts.remove(&from);
                        self.removes.remove(&to);
                        self.moves.push((from, to));
                    }
                    (Some(from), None) => self.remove(from),
                    (None, Some(to)) => self.upsert(to),
                    (None, None) => {}
                }
                true
            }
            _ => {
                for path in &event.paths {
                    if path.is_dir() || is_removed_folder(event, path) {
                        return false;
                    }

                    if let Some(path_in_vault) = note(path) {
                        if path.exists() {
                            self.upsert(path_in_vault);
                        } else {
                            self.remove(path_in_vault);
                        }
                    }
                }
                true
            }
        }
    }

    fn upsert(&mut self, path: String) {
        self.removes.remove(&path);
        self.upserts.insert(path);
    }

    fn remove(&mut self, path: String) {
        self.upserts.remove(&path);
        self.removes.insert(path);
    }
}

/// A removed or renamed-away path that no longer exists: when it has no file extension it was most likely a folder.
fn is_removed_folder(event: &DebouncedEvent, path: &Path) -> bool {
    let gone = matches!(
        event.kind,
        EventKind::Remove(RemoveKind::Folder | RemoveKind::Any)
            | EventKind::Modify(ModifyKind::Name(RenameMode::From))
    ) && !path.exists();

    gone && (matches!(event.kind, EventKind::Remove(RemoveKind::Folder))
        || path.extension().is_none())
}

fn split_by_size(notes: Vec<NoteFile>) -> Vec<Vec<NoteUpload>> {
    let mut batches = Vec::new();
    let mut batch = Vec::new();
    let mut bytes = 0;

    for note in notes {
        if !batch.is_empty() && bytes + note.content.len() > BYTES_PER_BATCH {
            batches.push(std::mem::take(&mut batch));
            bytes = 0;
        }

        bytes += note.content.len();
        batch.push(NoteUpload {
            path: note.path,
            hash: note.hash,
            content: note.content,
        });
    }

    if !batch.is_empty() {
        batches.push(batch);
    }

    batches
}

#[cfg(test)]
mod tests {
    use super::*;
    use notify_debouncer_full::notify::Event;
    use notify_debouncer_full::notify::event::CreateKind;
    use std::time::Instant as StdInstant;

    fn event(kind: EventKind, paths: &[&Path]) -> DebouncedEvent {
        let mut event = Event::new(kind);
        for path in paths {
            event = event.add_path(path.to_path_buf());
        }
        DebouncedEvent::new(event, StdInstant::now())
    }

    #[test]
    fn a_rename_between_notes_is_a_move_and_cancels_earlier_edits() {
        let root = Path::new("vault-that-does-not-exist");
        let filter = PathFilter::new(&[]).unwrap();
        let mut changes = Changes::default();

        changes.upsert("a.md".into());
        let ok = changes.record(
            root,
            &filter,
            &event(
                EventKind::Modify(ModifyKind::Name(RenameMode::Both)),
                &[&root.join("a.md"), &root.join("b.md")],
            ),
        );

        assert!(ok);
        assert!(changes.upserts.is_empty());
        assert_eq!(
            changes.moves,
            vec![("a.md".to_string(), "b.md".to_string())]
        );
    }

    #[test]
    fn a_vanished_note_is_removed_and_hidden_files_are_ignored() {
        let root = Path::new("vault-that-does-not-exist");
        let filter = PathFilter::new(&[]).unwrap();
        let mut changes = Changes::default();

        assert!(changes.record(
            root,
            &filter,
            &event(
                EventKind::Remove(RemoveKind::File),
                &[&root.join("gone.md")]
            )
        ));
        assert!(changes.record(
            root,
            &filter,
            &event(
                EventKind::Create(CreateKind::File),
                &[&root.join(".obsidian/workspace.json")]
            )
        ));

        assert_eq!(changes.removes.iter().collect::<Vec<_>>(), ["gone.md"]);
        assert!(changes.upserts.is_empty());
    }

    #[test]
    fn a_removed_folder_needs_a_full_reconcile() {
        let root = Path::new("vault-that-does-not-exist");
        let filter = PathFilter::new(&[]).unwrap();
        let mut changes = Changes::default();

        assert!(!changes.record(
            root,
            &filter,
            &event(
                EventKind::Remove(RemoveKind::Any),
                &[&root.join("Projects")]
            )
        ));
    }

    #[test]
    fn batches_split_before_the_byte_limit() {
        let note = |i: usize| NoteFile {
            path: format!("{i}.md"),
            hash: String::new(),
            content: "x".repeat(BYTES_PER_BATCH / 2 + 1),
        };

        let batches = split_by_size(vec![note(1), note(2), note(3)]);

        assert_eq!(batches.iter().map(Vec::len).collect::<Vec<_>>(), [1, 1, 1]);
    }
}
