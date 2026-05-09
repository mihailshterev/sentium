import { useRef, useState, useCallback } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  FolderOpen,
  FolderPlus,
  UploadCloud,
  CheckCircle,
  AlertCircle,
  Loader2,
  FileText,
  Pencil,
  Trash2,
  Plus,
  ChevronRight,
  X,
} from "lucide-react";
import styles from "./workspaces.module.scss";
import {
  fetchWorkspaces,
  createWorkspace,
  updateWorkspace,
  deleteWorkspace,
  fetchWorkspaceFiles,
  uploadWorkspaceFile,
  deleteWorkspaceFile,
} from "../../services/agentRuntime.service";
import type { Workspace, WorkspaceFile } from "../../types/workspace";

const STATUS_POLL_INTERVAL_MS = 4000;

const ALLOWED_EXTENSIONS = [
  ".txt",
  ".md",
  ".markdown",
  ".json",
  ".jsonl",
  ".csv",
  ".xml",
  ".yaml",
  ".yml",
  ".html",
  ".htm",
  ".log",
  ".py",
  ".js",
  ".ts",
  ".cs",
  ".java",
  ".sql",
  ".toml",
  ".ini",
  ".env",
  ".sh",
  ".bat",
  ".ps1",
  ".conf",
  ".cfg",
];

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "short", timeStyle: "short" });
}

const hasPending = (files: WorkspaceFile[]) =>
  files.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

const statusClass = (status: WorkspaceFile["processingStatus"]) => {
  switch (status) {
    case "Pending":
      return styles.pending;
    case "Processing":
      return styles.processing;
    case "Completed":
      return styles.completed;
    case "Failed":
      return styles.failed;
  }
};

interface WorkspaceFormProps {
  initial?: { name: string; description: string };
  onSubmit: (name: string, description: string) => void;
  onCancel: () => void;
  isPending: boolean;
  title: string;
}

const WorkspaceForm = ({ initial, onSubmit, onCancel, isPending, title }: WorkspaceFormProps) => {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (name.trim()) {
      onSubmit(name.trim(), description.trim());
    }
  };

  return (
    <div className={styles.modalOverlay} onClick={onCancel}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <span className={styles.modalTitle}>{title}</span>
          <button className={styles.modalClose} onClick={onCancel}>
            <X size={14} />
          </button>
        </div>
        <form onSubmit={handleSubmit} className={styles.modalForm}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-name">
              Name
            </label>
            <input
              id="ws-name"
              className={styles.input}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. incident-2026"
              autoFocus
              required
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-desc">
              Description
            </label>
            <textarea
              id="ws-desc"
              className={`${styles.input} ${styles.textarea}`}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description…"
              rows={3}
            />
          </div>
          <div className={styles.modalActions}>
            <button type="button" className={styles.cancelButton} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className={styles.submitButton} disabled={!name.trim() || isPending}>
              {isPending ? <Loader2 size={14} className={styles.spin} /> : null}
              {title}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

const Workspaces = () => {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [selectedWorkspace, setSelectedWorkspace] = useState<Workspace | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileTypeError, setFileTypeError] = useState<string | null>(null);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingWorkspace, setEditingWorkspace] = useState<Workspace | null>(null);

  const { data: workspaces = [], isError: isWorkspacesError } = useQuery({
    queryKey: ["workspaces"],
    queryFn: fetchWorkspaces,
  });

  const { mutate: createWs, isPending: isCreating } = useMutation({
    mutationFn: (payload: { name: string; description: string }) =>
      createWorkspace({ name: payload.name, description: payload.description || undefined }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      setShowCreateForm(false);
      setSelectedWorkspace(created);
    },
  });

  const { mutate: updateWs, isPending: isUpdating } = useMutation({
    mutationFn: ({ id, name, description }: { id: string; name: string; description: string }) =>
      updateWorkspace(id, { name, description: description || undefined }),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      setEditingWorkspace(null);
      if (selectedWorkspace?.id === updated.id) {
        setSelectedWorkspace(updated);
      }
    },
  });

  const { mutate: deleteWs } = useMutation({
    mutationFn: (id: string) => deleteWorkspace(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      if (selectedWorkspace?.id === id) {
        setSelectedWorkspace(null);
      }
    },
  });

  const { data: files = [], isError: isFilesError } = useQuery({
    queryKey: ["workspaceFiles", selectedWorkspace?.id],
    queryFn: () => fetchWorkspaceFiles(selectedWorkspace!.id),
    enabled: !!selectedWorkspace,
    refetchInterval: (query) => (query.state.data && hasPending(query.state.data) ? STATUS_POLL_INTERVAL_MS : false),
  });

  const {
    mutate: upload,
    isPending: isUploading,
    isSuccess: isUploadSuccess,
    isError: isUploadError,
    error: uploadError,
    reset: resetUpload,
  } = useMutation({
    mutationFn: (file: File) => uploadWorkspaceFile(file, selectedWorkspace?.id),
    onSuccess: () => {
      setSelectedFile(null);
      queryClient.invalidateQueries({ queryKey: ["workspaceFiles", selectedWorkspace?.id] });
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      setTimeout(() => resetUpload(), 4000);
    },
  });

  const { mutate: deleteFile } = useMutation({
    mutationFn: (fileId: string) => deleteWorkspaceFile(fileId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["workspaceFiles", selectedWorkspace?.id] });
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
    },
  });

  const validateAndSelect = (file: File) => {
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setFileTypeError(`File type "${ext}" is not supported.`);
      setSelectedFile(null);
      return;
    }
    setFileTypeError(null);
    setSelectedFile(file);
    resetUpload();
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
    const file = e.dataTransfer.files[0];
    if (file) {
      validateAndSelect(file);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  };
  const handleDragLeave = () => setDragActive(false);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      validateAndSelect(file);
    }
    e.target.value = "";
  };

  const handleUploadSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedFile && selectedWorkspace) {
      upload(selectedFile);
    }
  };

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <FolderOpen size={20} className={styles.titleIcon} />
          <div>
            <h2 className={styles.pageTitle}>Workspaces</h2>
            <span className={styles.pageSubtitle}>Organize files for agent access, RAG indexing, and analysis</span>
          </div>
        </div>
        <button className={styles.newButton} onClick={() => setShowCreateForm(true)}>
          <Plus size={14} />
          New Workspace
        </button>
      </div>

      <div className={styles.layout}>
        <main className={styles.detail}>
          {!selectedWorkspace ? (
            <div className={styles.emptyDetail}>
              <FolderOpen size={36} className={styles.emptyDetailIcon} />
              <p className={styles.emptyDetailText}>Select a workspace to manage its files</p>
            </div>
          ) : (
            <>
              <div className={styles.detailHeader}>
                <div>
                  <h3 className={styles.detailTitle}>{selectedWorkspace.name}</h3>
                  {selectedWorkspace.description && (
                    <p className={styles.detailDesc}>{selectedWorkspace.description}</p>
                  )}
                </div>
                <span className={styles.wsIdBadge} title={selectedWorkspace.id}>
                  ID: {selectedWorkspace.id.slice(0, 8)}…
                </span>
              </div>

              <div className={styles.detailBody}>
                <div className={styles.filesPanel}>
                  <p className={styles.panelTitle}>Files {files.length > 0 && `· ${files.length}`}</p>
                  <div className={styles.filesList}>
                    {isFilesError && <p className={styles.errorText}>Failed to load files.</p>}
                    {!isFilesError && files.length === 0 && (
                      <p className={styles.emptyMessage}>No files yet. Upload one using the panel on the right.</p>
                    )}
                    {files.map((f) => (
                      <div key={f.id} className={styles.fileCard}>
                        <FileText size={14} className={styles.fileIcon} />
                        <div className={styles.fileInfo}>
                          <span className={styles.fileName}>{f.fileName}</span>
                          <p className={styles.fileMeta}>
                            {formatBytes(f.sizeBytes)} · {formatDate(f.createdAt)}
                          </p>
                        </div>
                        <span className={`${styles.statusBadge} ${statusClass(f.processingStatus)}`}>
                          {f.processingStatus}
                        </span>
                        <button
                          className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                          title="Delete file"
                          onClick={() => deleteFile(f.id)}
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>

                <form className={styles.uploadPanel} onSubmit={handleUploadSubmit}>
                  <p className={styles.panelTitle}>Upload File</p>
                  <div
                    className={`${styles.uploadArea} ${dragActive ? styles.uploadAreaActive : ""}`}
                    onClick={() => fileInputRef.current?.click()}
                    onDrop={handleDrop}
                    onDragOver={handleDragOver}
                    onDragLeave={handleDragLeave}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => e.key === "Enter" && fileInputRef.current?.click()}
                  >
                    <UploadCloud size={28} className={styles.uploadIcon} />
                    {selectedFile ? (
                      <>
                        <span className={styles.uploadText}>{selectedFile.name}</span>
                        <span className={styles.uploadHint}>{formatBytes(selectedFile.size)}</span>
                      </>
                    ) : (
                      <>
                        <span className={styles.uploadText}>Drop a file or click to browse</span>
                        <span className={styles.uploadHint}>
                          Text files up to 100 MB — .txt .md .json .csv .yaml .py .ts …
                        </span>
                      </>
                    )}
                  </div>
                  <input
                    ref={fileInputRef}
                    type="file"
                    className={styles.fileInput}
                    onChange={handleInputChange}
                    accept={ALLOWED_EXTENSIONS.join(",")}
                  />

                  {fileTypeError && (
                    <div className={`${styles.statusBox} ${styles["status-error"]}`}>
                      <AlertCircle size={14} className={styles.statusIcon} />
                      <span className={styles.statusMessage}>{fileTypeError}</span>
                    </div>
                  )}

                  {isUploading && (
                    <div className={`${styles.statusBox} ${styles["status-loading"]}`}>
                      <Loader2 size={14} className={`${styles.statusIcon} ${styles.spin}`} />
                      <span className={styles.statusMessage}>Uploading…</span>
                    </div>
                  )}

                  {isUploadSuccess && (
                    <div className={`${styles.statusBox} ${styles["status-success"]}`}>
                      <CheckCircle size={14} className={styles.statusIcon} />
                      <span className={styles.statusMessage}>File uploaded — indexing in background.</span>
                    </div>
                  )}

                  {isUploadError && (
                    <div className={`${styles.statusBox} ${styles["status-error"]}`}>
                      <AlertCircle size={14} className={styles.statusIcon} />
                      <span className={styles.statusMessage}>
                        {uploadError instanceof Error ? uploadError.message : "Upload failed."}
                      </span>
                    </div>
                  )}

                  <button type="submit" className={styles.submitButton} disabled={!selectedFile || isUploading}>
                    {isUploading ? <Loader2 size={14} className={styles.spin} /> : <UploadCloud size={14} />}
                    Upload to {selectedWorkspace.name}
                  </button>
                </form>
              </div>
            </>
          )}
        </main>

        <aside className={styles.sidebar}>
          <p className={styles.sidebarTitle}>Workspaces {workspaces.length > 0 && `· ${workspaces.length}`}</p>
          {isWorkspacesError && <p className={styles.errorText}>Failed to load workspaces.</p>}
          {!isWorkspacesError && workspaces.length === 0 && (
            <div className={styles.emptyState}>
              <FolderPlus size={28} className={styles.emptyIcon} />
              <p>No workspaces yet.</p>
              <button className={styles.emptyAction} onClick={() => setShowCreateForm(true)}>
                Create one
              </button>
            </div>
          )}
          <div className={styles.workspaceList}>
            {workspaces.map((ws) => (
              <div
                key={ws.id}
                className={`${styles.workspaceCard} ${selectedWorkspace?.id === ws.id ? styles.workspaceCardActive : ""}`}
                onClick={() => setSelectedWorkspace(ws)}
              >
                <div className={styles.wsCardMain}>
                  <FolderOpen size={13} className={styles.wsIcon} />
                  <div className={styles.wsInfo}>
                    <span className={styles.wsName}>{ws.name}</span>
                    {ws.description && <span className={styles.wsDesc}>{ws.description}</span>}
                  </div>
                  <ChevronRight size={12} className={styles.wsChevron} />
                </div>
                <div className={styles.wsCardMeta}>
                  <span className={styles.wsFileCount}>
                    {ws.fileCount} file{ws.fileCount !== 1 ? "s" : ""}
                  </span>
                  <div className={styles.wsActions}>
                    <button
                      className={styles.iconBtn}
                      title="Edit workspace"
                      onClick={(e) => {
                        e.stopPropagation();
                        setEditingWorkspace(ws);
                      }}
                    >
                      <Pencil size={12} />
                    </button>
                    <button
                      className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                      title="Delete workspace"
                      onClick={(e) => {
                        e.stopPropagation();
                        deleteWs(ws.id);
                      }}
                    >
                      <Trash2 size={12} />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </aside>
      </div>

      {showCreateForm && (
        <WorkspaceForm
          title="Create Workspace"
          isPending={isCreating}
          onSubmit={(name, description) => createWs({ name, description })}
          onCancel={() => setShowCreateForm(false)}
        />
      )}

      {editingWorkspace && (
        <WorkspaceForm
          title="Edit Workspace"
          initial={{ name: editingWorkspace.name, description: editingWorkspace.description ?? "" }}
          isPending={isUpdating}
          onSubmit={(name, description) => updateWs({ id: editingWorkspace.id, name, description })}
          onCancel={() => setEditingWorkspace(null)}
        />
      )}
    </div>
  );
};

export default Workspaces;
