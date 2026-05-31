import { useRef, useState, useCallback } from "react";
import { useNavigate, useParams } from "react-router";
import {
  ArrowLeft,
  FolderOpen,
  FileText,
  Trash2,
  Pencil,
  Loader2,
  UploadCloud,
  CheckCircle,
  AlertCircle,
} from "lucide-react";
import styles from "./workspaces.module.scss";
import useWorkspaces from "../../hooks/useWorkspaces";
import useWorkspaceFiles from "../../hooks/useWorkspaceFiles";
import type { WorkspaceFile } from "../../types/workspace";
import PageHeader from "../../components/ui/page-header";
import WorkspaceForm from "./components/workspace-form";
import ConfirmDialog from "../../components/ui/confirm-dialog";
import { formatBytesToMb, formatDateTimeShort } from "../../utils/formatters";

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

const statusBadgeClass = (status: WorkspaceFile["processingStatus"]) => {
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

const WorkspaceDetail = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [dragActive, setDragActive] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileTypeError, setFileTypeError] = useState<string | null>(null);
  const [showEditForm, setShowEditForm] = useState(false);
  const [pendingDeleteFileId, setPendingDeleteFileId] = useState<string | null>(null);
  const [pendingDeleteFileName, setPendingDeleteFileName] = useState<string | null>(null);

  const { workspaces, updateWorkspace, isUpdatingWorkspace } = useWorkspaces();
  const workspace = workspaces.find((w) => w.id === workspaceId) ?? null;

  const {
    files,
    isFilesLoading,
    isFilesError,
    uploadFile,
    isUploading,
    isUploadSuccess,
    isUploadError,
    uploadError,
    resetUpload,
    deleteFile,
  } = useWorkspaceFiles(workspaceId);

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
    if (file) validateAndSelect(file);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  };
  const handleDragLeave = () => setDragActive(false);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) validateAndSelect(file);
    e.target.value = "";
  };

  const handleUploadSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedFile && workspaceId)
      uploadFile(selectedFile, {
        onSuccess: () => {
          setSelectedFile(null);
          setTimeout(() => resetUpload(), 4000);
        },
      });
  };

  if (!workspaceId) {
    navigate("/workspaces");
    return null;
  }

  return (
    <div className={styles.detailRoot}>
      <div className={styles.pipelineBg} aria-hidden="true">
        <div className={styles.bgGrid} />
        <div className={styles.bgOrb1} />
        <div className={styles.bgOrb2} />
        <div className={styles.bgOrb3} />
      </div>

      <PageHeader
        icon={
          <button className={styles.backBtn} onClick={() => navigate("/workspaces")} title="Back to workspaces">
            <ArrowLeft size={15} />
          </button>
        }
        title={workspace?.name ?? "Workspace"}
        subtitle={workspace?.description ?? "Loading workspace…"}
        right={
          workspace ? (
            <div className={styles.headerRight}>
              <span className={styles.wsIdBadge} title={workspace.id}>
                ID: {workspace.id.slice(0, 8)}…
              </span>
              <button className={styles.detailEditBtn} onClick={() => setShowEditForm(true)}>
                <Pencil size={13} />
                Edit
              </button>
            </div>
          ) : undefined
        }
      />

      <div className={styles.detailBody}>
        <div className={styles.fileTreePanel}>
          <div className={styles.fileTreeHeader}>
            <div className={styles.fileTreeHeaderLeft}>
              <div className={styles.fileTreeRoot}>
                <FolderOpen size={16} className={styles.fileTreeRootIcon} />
                <span>{workspace?.name ?? "…"}/</span>
              </div>
              {files.length > 0 && (
                <span className={styles.fileCountPill}>
                  {files.length} file{files.length !== 1 ? "s" : ""}
                </span>
              )}
            </div>
          </div>

          <div className={styles.fileTreeScroll}>
            {isFilesLoading && (
              <div className={styles.fileTreeEmpty}>
                <Loader2 size={22} className={styles.spin} />
              </div>
            )}
            {isFilesError && <p className={styles.fileTreeError}>Failed to load files.</p>}

            {!isFilesLoading && !isFilesError && files.length === 0 && (
              <div className={styles.fileTreeEmpty}>
                <FileText size={32} className={styles.fileTreeEmptyIcon} />
                <p>No files yet</p>
                <span style={{ fontSize: "0.72rem", color: "var(--text-dim)" }}>
                  Upload a file using the panel on the right
                </span>
              </div>
            )}

            {files.length > 0 && (
              <div className={styles.fileTreeBranch}>
                {files.map((f) => (
                  <FileTreeItem
                    key={f.id}
                    file={f}
                    onDelete={(id, name) => {
                      setPendingDeleteFileId(id);
                      setPendingDeleteFileName(name);
                    }}
                  />
                ))}
              </div>
            )}
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
                <span className={styles.uploadHint}>{formatBytesToMb(selectedFile.size)}</span>
              </>
            ) : (
              <>
                <span className={styles.uploadText}>Drop a file or click to browse</span>
                <span className={styles.uploadHint}>Text files up to 100 MB · .txt .md .json .csv .yaml .py .ts …</span>
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
            Upload to {workspace?.name ?? "workspace"}
          </button>
        </form>
      </div>

      {showEditForm && workspace && (
        <WorkspaceForm
          title="Edit Workspace"
          initial={{ name: workspace.name, description: workspace.description ?? "" }}
          isPending={isUpdatingWorkspace}
          onSubmit={(name, description) =>
            updateWorkspace(
              { id: workspaceId!, name, description: description || undefined },
              { onSuccess: () => setShowEditForm(false) },
            )
          }
          onCancel={() => setShowEditForm(false)}
        />
      )}

      <ConfirmDialog
        open={pendingDeleteFileId !== null}
        variant="danger"
        title="Remove File"
        description={`Permanently remove "${pendingDeleteFileName}"? Its contents will be removed from the indexed vector store.`}
        confirmLabel="Remove file"
        onConfirm={() =>
          pendingDeleteFileId &&
          deleteFile(pendingDeleteFileId, {
            onSuccess: () => {
              setPendingDeleteFileId(null);
              setPendingDeleteFileName(null);
            },
          })
        }
        onCancel={() => {
          setPendingDeleteFileId(null);
          setPendingDeleteFileName(null);
        }}
      />
    </div>
  );
};

interface FileTreeItemProps {
  file: WorkspaceFile;
  onDelete: (id: string, name: string) => void;
}

const FileTreeItem = ({ file, onDelete }: FileTreeItemProps) => (
  <div className={styles.fileItem}>
    <FileText size={13} className={styles.fileItemIcon} />
    <div className={styles.fileItemInfo}>
      <span className={styles.fileItemName}>{file.fileName}</span>
      <span className={styles.fileItemMeta}>
        {formatBytesToMb(file.sizeBytes)} · {formatDateTimeShort(file.createdAt)}
      </span>
    </div>
    <span className={`${styles.fileItemBadge} ${statusBadgeClass(file.processingStatus)}`}>
      {file.processingStatus}
    </span>
    <button className={styles.fileItemDeleteBtn} title="Remove file" onClick={() => onDelete(file.id, file.fileName)}>
      <Trash2 size={12} />
    </button>
  </div>
);

export default WorkspaceDetail;
