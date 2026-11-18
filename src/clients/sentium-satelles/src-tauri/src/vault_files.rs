//! The vault on disk: which files count as notes, their vault-relative paths and their hashes. Pure file-system code,
//! no network, so it is unit tested on its own.

use std::fs;
use std::path::{Component, Path};

use globset::{GlobBuilder, GlobSet, GlobSetBuilder};
use sha2::{Digest, Sha256};
use walkdir::WalkDir;

/// The server refuses notes over 1,000,000 characters; a UTF-8 file this size never has more characters than bytes.
pub const MAX_NOTE_BYTES: u64 = 1_000_000;

/// Obsidian keeps its own state in dot-folders (`.obsidian`, `.trash`) and never shows them as notes.
const HIDDEN_PREFIX: char = '.';
const NOTE_EXTENSION: &str = ".md";

/// A note's vault-relative path and the lower-case hex SHA-256 of its bytes.
pub struct NoteHash {
    pub path: String,
    pub hash: String,
}

pub struct NoteFile {
    pub path: String,
    pub hash: String,
    pub content: String,
}

/// Decides which vault files are synced: markdown files outside hidden folders that match no exclude pattern.
pub struct PathFilter {
    excludes: GlobSet,
}

impl PathFilter {
    /// `patterns` are globs on the vault-relative path (`Private/**`, `*.excalidraw.md`). A pattern without `/` also
    /// matches that name in any folder, and a folder pattern excludes everything below it.
    pub fn new(patterns: &[String]) -> Result<Self, String> {
        let mut builder = GlobSetBuilder::new();

        for pattern in patterns {
            let pattern = pattern.trim().trim_matches('/');
            if pattern.is_empty() {
                continue;
            }

            let mut variants = vec![pattern.to_string(), format!("{pattern}/**")];
            if !pattern.contains('/') {
                variants.push(format!("**/{pattern}"));
                variants.push(format!("**/{pattern}/**"));
            }

            for variant in variants {
                let glob = GlobBuilder::new(&variant)
                    .literal_separator(true)
                    .case_insensitive(true)
                    .build()
                    .map_err(|e| format!("'{pattern}' is not a valid pattern: {e}"))?;
                builder.add(glob);
            }
        }

        let excludes = builder
            .build()
            .map_err(|e| format!("The exclude patterns are invalid: {e}"))?;
        Ok(Self { excludes })
    }

    /// Whether `path` (vault-relative, `/`-separated) is a note to sync.
    pub fn is_note(&self, path: &str) -> bool {
        path.to_ascii_lowercase().ends_with(NOTE_EXTENSION)
            && !path
                .split('/')
                .any(|segment| segment.starts_with(HIDDEN_PREFIX))
            && !self.excludes.is_match(path)
    }

    /// Whether the scan should descend into the folder at `path`.
    fn walks_into(&self, path: &str) -> bool {
        !path
            .split('/')
            .any(|segment| segment.starts_with(HIDDEN_PREFIX))
            && !self.excludes.is_match(path)
    }
}

/// `absolute` as a vault-relative path with `/` separators, or `None` outside the vault or for names that are not
/// valid Unicode.
pub fn relative_path(root: &Path, absolute: &Path) -> Option<String> {
    let relative = absolute.strip_prefix(root).ok()?;
    let mut parts = Vec::new();

    for component in relative.components() {
        match component {
            Component::Normal(name) => parts.push(name.to_str()?.to_string()),
            _ => return None,
        }
    }

    (!parts.is_empty()).then(|| parts.join("/"))
}

pub fn sha256_hex(bytes: &[u8]) -> String {
    Sha256::digest(bytes)
        .iter()
        .map(|b| format!("{b:02x}"))
        .collect()
}

/// Every note in the vault with its hash. Unreadable, oversized or non-text files are skipped (and logged), so one bad
/// file never stops the sync.
pub fn scan(root: &Path, filter: &PathFilter) -> Vec<NoteHash> {
    let walker = WalkDir::new(root)
        .follow_links(false)
        .into_iter()
        .filter_entry(|entry| {
            entry.depth() == 0
                || !entry.file_type().is_dir()
                || relative_path(root, entry.path()).is_some_and(|path| filter.walks_into(&path))
        });

    let mut notes = Vec::new();
    for entry in walker.filter_map(Result::ok) {
        if !entry.file_type().is_file() {
            continue;
        }

        let Some(path) = relative_path(root, entry.path()).filter(|p| filter.is_note(p)) else {
            continue;
        };

        if let Some(note) = read_note(root, &path) {
            notes.push(NoteHash {
                path: note.path,
                hash: note.hash,
            });
        }
    }

    notes.sort_by(|a, b| a.path.cmp(&b.path));
    notes
}

/// The note at `path`, or `None` when it is gone, too large, or not text the server can store.
pub fn read_note(root: &Path, path: &str) -> Option<NoteFile> {
    let absolute = root.join(path);
    let metadata = fs::metadata(&absolute).ok()?;
    if !metadata.is_file() {
        return None;
    }

    if metadata.len() > MAX_NOTE_BYTES {
        log::info!("skipping {path}: larger than {MAX_NOTE_BYTES} bytes");
        return None;
    }

    let bytes = fs::read(&absolute).ok()?;
    let hash = sha256_hex(&bytes);
    match String::from_utf8(bytes) {
        Ok(content) if !content.contains('\0') => Some(NoteFile {
            path: path.to_string(),
            hash,
            content,
        }),
        _ => {
            log::info!("skipping {path}: not UTF-8 text");
            None
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn filter(patterns: &[&str]) -> PathFilter {
        PathFilter::new(&patterns.iter().map(|p| p.to_string()).collect::<Vec<_>>()).unwrap()
    }

    #[test]
    fn only_markdown_outside_hidden_folders_is_a_note() {
        let filter = filter(&[]);
        assert!(filter.is_note("Daily/2026-11-18.md"));
        assert!(filter.is_note("Readme.MD"));
        assert!(!filter.is_note("image.png"));
        assert!(!filter.is_note(".obsidian/workspace.md"));
        assert!(!filter.is_note("Notes/.trash/old.md"));
    }

    #[test]
    fn excludes_match_folders_anywhere_and_globs_by_path() {
        let filter = filter(&["Templates/", "Private/**", "*.excalidraw.md"]);
        assert!(!filter.is_note("Templates/Daily.md"));
        assert!(!filter.is_note("Areas/Templates/Weekly.md"));
        assert!(!filter.is_note("Private/Diary/Today.md"));
        assert!(!filter.is_note("Drawing.excalidraw.md"));
        assert!(filter.is_note("Areas/Health.md"));
    }

    #[test]
    fn a_star_does_not_cross_folders() {
        let filter = filter(&["Archive/*.md"]);
        assert!(!filter.is_note("Archive/Old.md"));
        assert!(filter.is_note("Archive/2025/Old.md"));
        assert!(filter.is_note("Projects/Archive.md"));
    }

    #[test]
    fn invalid_patterns_are_reported() {
        assert!(PathFilter::new(&["[".to_string()]).is_err());
    }

    #[test]
    fn relative_paths_use_forward_slashes() {
        let root = Path::new("vault");
        assert_eq!(
            relative_path(root, &root.join("a").join("b.md")).as_deref(),
            Some("a/b.md")
        );
        assert_eq!(relative_path(root, Path::new("elsewhere/b.md")), None);
    }

    #[test]
    fn hashes_match_the_server() {
        assert_eq!(
            sha256_hex(b"hello"),
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824"
        );
    }

    #[test]
    fn scan_hashes_notes_and_skips_hidden_and_excluded_files() {
        let root = std::env::temp_dir().join(format!("satelles-vault-{}", fastrand::u64(..)));
        fs::create_dir_all(root.join(".obsidian")).unwrap();
        fs::create_dir_all(root.join("Private")).unwrap();
        fs::create_dir_all(root.join("Notes")).unwrap();
        fs::write(root.join(".obsidian/app.md"), "x").unwrap();
        fs::write(root.join("Private/secret.md"), "x").unwrap();
        fs::write(root.join("Notes/a.md"), "hello").unwrap();
        fs::write(root.join("Notes/binary.md"), [0xff, 0xfe, 0x00]).unwrap();
        fs::write(root.join("Top.md"), "top").unwrap();

        let notes = scan(&root, &filter(&["Private"]));
        fs::remove_dir_all(&root).unwrap();

        let paths: Vec<_> = notes.iter().map(|n| n.path.as_str()).collect();
        assert_eq!(paths, ["Notes/a.md", "Top.md"]);
        assert_eq!(notes[0].hash, sha256_hex(b"hello"));
    }
}
