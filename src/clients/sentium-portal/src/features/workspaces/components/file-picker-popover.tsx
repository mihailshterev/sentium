import { useEffect, useEffectEvent, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, ChevronRight, FileText, FolderOpen, Loader2, Upload } from "lucide-react";
import { fetchWorkspaceFiles, fetchWorkspaces, listWorkspaceFiles } from "@/features/workspaces/api/workspaces.service";
import type { Workspace, WorkspaceFile } from "@/features/workspaces/types/workspace";
import { formatBytesToMb } from "@/shared/utils/formatters";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";
import { FileStatusBadge } from "./file-status-badge";
import styles from "./file-picker-popover.module.css";

const ALL_FILES_KEY = ["workspaceFiles"] as const;

const isProcessing = (files: WorkspaceFile[] | undefined) =>
  !!files?.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

interface FilePickerPopoverProps {
  open: boolean;
  onClose: () => void;
  /** Listed first and expanded on open, e.g. the conversation's workspace. */
  preferredWorkspaceId?: string | null;
  selectedIds: readonly string[];
  onToggleFile: (file: WorkspaceFile) => void;
  /** Shows an "Upload from computer" row when given. */
  onUploadClick?: () => void;
  /** Clicks inside this element (e.g. the button that toggles the popover) do not count as clicking outside. */
  anchorRef?: React.RefObject<HTMLElement | null>;
  className?: string;
}

/**
 * Picks workspace files to attach. Files can be attached in any processing state: their text is read directly, so
 * search indexing does not need to have finished.
 */
export function FilePickerPopover({ open, ...panel }: FilePickerPopoverProps) {
  // Mounted only while open, so every opening starts with a clear search and the preferred workspace expanded.
  return open ? <PickerPanel {...panel} /> : null;
}

function PickerPanel({
  onClose,
  preferredWorkspaceId,
  selectedIds,
  onToggleFile,
  onUploadClick,
  anchorRef,
  className,
}: Omit<FilePickerPopoverProps, "open">) {
  const ref = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [expanded, setExpanded] = useState<string | null>(preferredWorkspaceId ?? null);

  useRealtimeInvalidate(REALTIME_TOPICS.workspaceFiles, [ALL_FILES_KEY]);

  const workspacesQuery = useQuery({ queryKey: ["workspaces", "options"], queryFn: fetchWorkspaces });
  const searching = query.trim().length > 0;
  const allFilesQuery = useQuery({
    queryKey: ["workspaceFiles", "all"],
    queryFn: () => listWorkspaceFiles(),
    enabled: searching,
  });

  const close = useEffectEvent(() => onClose());

  useEffect(() => {
    searchRef.current?.focus();

    const onPointerDown = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!ref.current?.contains(target) && !anchorRef?.current?.contains(target)) {
        close();
      }
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        close();
      }
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [anchorRef]);

  const workspaces = useMemo(() => {
    const list = workspacesQuery.data ?? [];
    return [...list].sort((a, b) => Number(b.id === preferredWorkspaceId) - Number(a.id === preferredWorkspaceId));
  }, [workspacesQuery.data, preferredWorkspaceId]);

  const workspaceNames = useMemo(() => new Map(workspaces.map((w) => [w.id, w.name])), [workspaces]);

  const matches = useMemo(() => {
    const term = query.trim().toLowerCase();
    return (allFilesQuery.data ?? []).filter((f) => f.fileName.toLowerCase().includes(term));
  }, [allFilesQuery.data, query]);

  return (
    <div ref={ref} className={`${styles.popover} ${className ?? ""}`} role="dialog" aria-label="Attach workspace files">
      <input
        ref={searchRef}
        type="search"
        className={styles.search}
        placeholder="Search files…"
        aria-label="Search files"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />

      {onUploadClick && (
        <button type="button" className={styles.uploadRow} onClick={onUploadClick}>
          <Upload size={14} aria-hidden="true" />
          <span>Upload from computer…</span>
        </button>
      )}

      <div className={styles.list}>
        {searching ? (
          <SearchResults
            loading={allFilesQuery.isLoading}
            files={matches}
            workspaceNames={workspaceNames}
            selectedIds={selectedIds}
            onToggleFile={onToggleFile}
          />
        ) : (
          <>
            {workspacesQuery.isLoading && <PickerStatus busy>Loading workspaces…</PickerStatus>}
            {workspacesQuery.isError && <PickerStatus error>Workspaces could not be loaded.</PickerStatus>}
            {workspacesQuery.isSuccess && workspaces.length === 0 && <PickerStatus>No workspaces yet.</PickerStatus>}
            {workspaces.map((ws) => (
              <WorkspaceGroup
                key={ws.id}
                workspace={ws}
                current={ws.id === preferredWorkspaceId}
                expanded={expanded === ws.id}
                onToggle={() => setExpanded((v) => (v === ws.id ? null : ws.id))}
                selectedIds={selectedIds}
                onToggleFile={onToggleFile}
              />
            ))}
          </>
        )}
      </div>
    </div>
  );
}

function WorkspaceGroup({
  workspace,
  current,
  expanded,
  onToggle,
  selectedIds,
  onToggleFile,
}: {
  workspace: Workspace;
  current: boolean;
  expanded: boolean;
  onToggle: () => void;
  selectedIds: readonly string[];
  onToggleFile: (file: WorkspaceFile) => void;
}) {
  const filesQuery = useQuery({
    queryKey: ["workspaceFiles", workspace.id],
    queryFn: () => fetchWorkspaceFiles(workspace.id),
    enabled: expanded,
    refetchInterval: (query) => (isProcessing(query.state.data) ? REALTIME_FALLBACK_POLL_MS : false),
  });

  return (
    <div className={styles.group}>
      <button type="button" className={styles.groupHeader} onClick={onToggle} aria-expanded={expanded}>
        <ChevronRight size={13} className={expanded ? styles.chevronOpen : styles.chevron} aria-hidden="true" />
        <FolderOpen size={14} aria-hidden="true" />
        <span className={styles.groupName}>{workspace.name}</span>
        {current && <span className={styles.currentTag}>this chat</span>}
        <span className={styles.count}>{workspace.fileCount}</span>
      </button>
      {expanded && (
        <div className={styles.files}>
          {filesQuery.isLoading && <PickerStatus busy>Loading files…</PickerStatus>}
          {filesQuery.isError && <PickerStatus error>Files could not be loaded.</PickerStatus>}
          {filesQuery.isSuccess && filesQuery.data.length === 0 && <PickerStatus>No files.</PickerStatus>}
          {filesQuery.data?.map((f) => (
            <FileRow key={f.id} file={f} selected={selectedIds.includes(f.id)} onToggle={() => onToggleFile(f)} />
          ))}
        </div>
      )}
    </div>
  );
}

function SearchResults({
  loading,
  files,
  workspaceNames,
  selectedIds,
  onToggleFile,
}: {
  loading: boolean;
  files: WorkspaceFile[];
  workspaceNames: Map<string, string>;
  selectedIds: readonly string[];
  onToggleFile: (file: WorkspaceFile) => void;
}) {
  if (loading) {
    return <PickerStatus busy>Searching…</PickerStatus>;
  }
  if (files.length === 0) {
    return <PickerStatus>No matching files.</PickerStatus>;
  }
  return (
    <div className={styles.files}>
      {files.map((f) => (
        <FileRow
          key={f.id}
          file={f}
          hint={f.workspaceId ? workspaceNames.get(f.workspaceId) : undefined}
          selected={selectedIds.includes(f.id)}
          onToggle={() => onToggleFile(f)}
        />
      ))}
    </div>
  );
}

function FileRow({
  file,
  hint,
  selected,
  onToggle,
}: {
  file: WorkspaceFile;
  hint?: string;
  selected: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      className={styles.file}
      onClick={onToggle}
      aria-pressed={selected}
      title={file.processingStatus === "Failed" && file.processingError ? file.processingError : file.fileName}
    >
      <span className={styles.check} aria-hidden="true">
        {selected ? <Check size={12} /> : <FileText size={12} />}
      </span>
      <span className={styles.fileName}>{file.fileName}</span>
      <span className={styles.fileMeta}>{hint ?? formatBytesToMb(file.sizeBytes)}</span>
      {file.processingStatus !== "Completed" && <FileStatusBadge status={file.processingStatus} />}
    </button>
  );
}

function PickerStatus({ children, error, busy }: { children: React.ReactNode; error?: boolean; busy?: boolean }) {
  return (
    <p className={error ? `${styles.status} ${styles.statusError}` : styles.status} role={error ? "alert" : undefined}>
      {busy && <Loader2 size={12} className="animate-spin" aria-hidden="true" />}
      {children}
    </p>
  );
}
