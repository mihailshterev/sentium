import { useCallback, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, FileText, FolderOpen, Pencil, Trash2 } from "lucide-react";
import styles from "../workspaces.module.css";
import { useWorkspaces } from "@/features/workspaces/hooks/useWorkspaces";
import { useWorkspaceFiles } from "@/features/workspaces/hooks/useWorkspaceFiles";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatBytesToMb, formatDateTimeShort } from "@/shared/utils/formatters";
import { FileStatusBadge } from "../components/file-status-badge";
import { UploadPanel } from "../components/upload-panel";
import { WorkspaceForm } from "../components/workspace-form";

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

export function WorkspaceDetailView() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [dragActive, setDragActive] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileTypeError, setFileTypeError] = useState<string | null>(null);
  const [showEditForm, setShowEditForm] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<{ id: string; name: string } | null>(null);

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
    if (file) {
      validateAndSelect(file);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleUploadSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedFile && workspaceId) {
      uploadFile(selectedFile, {
        onSuccess: () => {
          setSelectedFile(null);
          setTimeout(() => resetUpload(), 4000);
        },
      });
    }
  };

  if (!workspaceId) {
    navigate("/workspaces");
    return null;
  }

  return (
    <Page
      title={workspace?.name ?? "Workspace"}
      subtitle={workspace?.description ?? "Loading workspace…"}
      width="wide"
      leading={
        <Button
          variant="secondary"
          size="sm"
          iconOnly
          icon={<ArrowLeft size={16} />}
          onClick={() => navigate("/workspaces")}
          title="Back to workspaces"
        />
      }
      actions={
        workspace ? (
          <>
            <Badge tone="neutral" title={workspace.id} className={styles.idBadge}>
              ID: {workspace.id.slice(0, 8)}…
            </Badge>
            <Button variant="secondary" icon={<Pencil size={15} />} onClick={() => setShowEditForm(true)}>
              Edit
            </Button>
          </>
        ) : undefined
      }
    >
      <div className={styles.detailLayout}>
        <Card padding="none" as="section">
          <CardHeader
            icon={<FolderOpen />}
            title={`${workspace?.name ?? "…"}/`}
            actions={
              files.length > 0 && (
                <Badge tone="neutral">
                  {files.length} file{files.length !== 1 ? "s" : ""}
                </Badge>
              )
            }
          />

          {isFilesLoading && (
            <div className={styles.filesPadding} aria-busy="true">
              <SkeletonText lines={3} />
            </div>
          )}
          {isFilesError && (
            <div className={styles.filesPadding}>
              <StatusMessage variant="error" message="Failed to load files." />
            </div>
          )}

          {!isFilesLoading && !isFilesError && files.length === 0 && (
            <EmptyState icon={<FileText />} title="No files yet" hint="Upload a file using the panel on the right" />
          )}

          {files.length > 0 && (
            <ul className={styles.fileList}>
              {files.map((f) => (
                <FileRow key={f.id} file={f} onDelete={(id, name) => setPendingDelete({ id, name })} />
              ))}
            </ul>
          )}
        </Card>

        <UploadPanel
          workspaceName={workspace?.name ?? "workspace"}
          selectedFile={selectedFile}
          dragActive={dragActive}
          fileTypeError={fileTypeError}
          isUploading={isUploading}
          isUploadSuccess={isUploadSuccess}
          isUploadError={isUploadError}
          uploadError={uploadError}
          acceptedExtensions={ALLOWED_EXTENSIONS.join(",")}
          onDrop={handleDrop}
          onDragOver={(e) => {
            e.preventDefault();
            setDragActive(true);
          }}
          onDragLeave={() => setDragActive(false)}
          onInputChange={(e) => {
            const file = e.target.files?.[0];
            if (file) {
              validateAndSelect(file);
            }
            e.target.value = "";
          }}
          onSubmit={handleUploadSubmit}
          fileInputRef={fileInputRef}
        />
      </div>

      {showEditForm && workspace && (
        <WorkspaceForm
          title="Edit Workspace"
          initial={{ name: workspace.name, description: workspace.description ?? "" }}
          isPending={isUpdatingWorkspace}
          onSubmit={(name, description) =>
            updateWorkspace(
              { id: workspaceId, name, description: description || undefined },
              { onSuccess: () => setShowEditForm(false) },
            )
          }
          onCancel={() => setShowEditForm(false)}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        variant="danger"
        title="Remove File"
        description={`Permanently remove "${pendingDelete?.name}"? Its contents will be removed from the indexed vector store.`}
        confirmLabel="Remove file"
        onConfirm={() => pendingDelete && deleteFile(pendingDelete.id, { onSuccess: () => setPendingDelete(null) })}
        onCancel={() => setPendingDelete(null)}
      />
    </Page>
  );
}

interface FileRowProps {
  file: WorkspaceFile;
  onDelete: (id: string, name: string) => void;
}

const FileRow = ({ file, onDelete }: FileRowProps) => (
  <li className={styles.fileRow}>
    <FileText size={16} className={styles.fileIcon} aria-hidden="true" />
    <div className={styles.fileInfo}>
      <span className={styles.fileName}>{file.fileName}</span>
      <span className={styles.fileMeta}>
        {formatBytesToMb(file.sizeBytes)} · {formatDateTimeShort(file.createdAt)}
      </span>
    </div>
    <FileStatusBadge status={file.processingStatus} />
    <Button
      variant="ghost"
      size="sm"
      iconOnly
      icon={<Trash2 size={15} />}
      title="Remove file"
      onClick={() => onDelete(file.id, file.fileName)}
      className={styles.deleteButton}
    />
  </li>
);
