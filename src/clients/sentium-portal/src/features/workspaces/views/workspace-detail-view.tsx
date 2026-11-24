import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Download, Eye, FileText, FolderOpen, Pencil, RotateCw, Trash2 } from "lucide-react";
import styles from "../workspaces.module.css";
import { fetchWorkspace, workspaceFileContentUrl } from "@/features/workspaces/api/workspaces.service";
import { useWorkspaces } from "@/features/workspaces/hooks/useWorkspaces";
import { useWorkspaceFiles } from "@/features/workspaces/hooks/useWorkspaceFiles";
import { useUploadRules } from "@/features/workspaces/hooks/useUploadRules";
import { uploadProblem } from "@/features/workspaces/utils/upload-validation";
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
import { useDebouncedValue } from "@/shared/hooks/useDebouncedValue";
import { SearchInput } from "@/shared/ui/form-controls";
import { LoadMore } from "@/shared/ui/load-more";
import { FilePreviewDialog } from "../components/file-preview-dialog";
import { FileStatusBadge } from "../components/file-status-badge";
import { UploadPanel } from "../components/upload-panel";
import { WorkspaceForm } from "../components/workspace-form";

const SUCCESS_MESSAGE_MS = 4000;

export function WorkspaceDetailView() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const rules = useUploadRules();

  const [dragActive, setDragActive] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [problems, setProblems] = useState<string[]>([]);
  const [showEditForm, setShowEditForm] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<{ id: string; name: string } | null>(null);
  const [previewing, setPreviewing] = useState<WorkspaceFile | null>(null);
  const [fileSearch, setFileSearch] = useState("");
  const debouncedFileSearch = useDebouncedValue(fileSearch);

  const { updateWorkspace, isUpdatingWorkspace } = useWorkspaces();
  const { data: workspace, isError: isWorkspaceError } = useQuery({
    queryKey: ["workspaces", "detail", workspaceId],
    queryFn: () => fetchWorkspace(workspaceId!),
    enabled: !!workspaceId,
  });

  const {
    files,
    totalCount,
    hasMoreFiles,
    loadMoreFiles,
    isLoadingMoreFiles,
    isFilesLoading,
    isFilesError,
    uploadFiles,
    isUploading,
    uploadProgress,
    isUploadSuccess,
    isUploadError,
    uploadError,
    resetUpload,
    deleteFile,
    reindexFile,
    reindexingFileId,
  } = useWorkspaceFiles(workspaceId, debouncedFileSearch);
  const searchingFiles = debouncedFileSearch.trim().length > 0;

  useEffect(() => {
    if (!workspaceId) {
      navigate("/workspaces", { replace: true });
    }
  }, [workspaceId, navigate]);

  useEffect(() => {
    if (!isUploadSuccess) {
      return;
    }
    const timer = setTimeout(resetUpload, SUCCESS_MESSAGE_MS);
    return () => clearTimeout(timer);
  }, [isUploadSuccess, resetUpload]);

  const select = (chosen: readonly File[]) => {
    const accepted: File[] = [];
    const rejected: string[] = [];
    for (const file of chosen) {
      const problem = uploadProblem(file, rules);
      if (problem) {
        rejected.push(problem);
      } else {
        accepted.push(file);
      }
    }
    setProblems(rejected);
    setSelectedFiles(accepted);
    resetUpload();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
    select(Array.from(e.dataTransfer.files));
  };

  const handleUploadSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedFiles.length > 0 && workspaceId) {
      uploadFiles(selectedFiles, { onSuccess: () => setSelectedFiles([]) });
    }
  };

  if (!workspaceId) {
    return null;
  }

  return (
    <Page
      title={workspace?.name ?? "Workspace"}
      subtitle={workspace ? (workspace.description ?? undefined) : isWorkspaceError ? "Workspace not found" : "Loading workspace…"}
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
              totalCount > 0 && (
                <Badge tone="neutral">
                  {totalCount} file{totalCount !== 1 ? "s" : ""}
                </Badge>
              )
            }
          />

          {(totalCount > 0 || searchingFiles) && (
            <div className={styles.fileSearch}>
              <SearchInput
                controlSize="sm"
                placeholder="Search files by name"
                aria-label="Search files"
                value={fileSearch}
                onChange={(e) => setFileSearch(e.target.value)}
              />
            </div>
          )}

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
            <EmptyState
              icon={<FileText />}
              title={searchingFiles ? `No files match “${debouncedFileSearch.trim()}”` : "No files yet"}
              hint={searchingFiles ? undefined : "Upload files using the panel on the right"}
            />
          )}

          {files.length > 0 && (
            <ul className={styles.fileList}>
              {files.map((f) => (
                <FileRow
                  key={f.id}
                  file={f}
                  reindexing={reindexingFileId === f.id}
                  onPreview={setPreviewing}
                  onRetry={reindexFile}
                  onDelete={(id, name) => setPendingDelete({ id, name })}
                />
              ))}
            </ul>
          )}

          <LoadMore hasMore={hasMoreFiles} isLoading={isLoadingMoreFiles} onLoadMore={loadMoreFiles} />
        </Card>

        <UploadPanel
          workspaceName={workspace?.name ?? "workspace"}
          selectedFiles={selectedFiles}
          dragActive={dragActive}
          problems={problems}
          isUploading={isUploading}
          uploadProgress={uploadProgress}
          isUploadSuccess={isUploadSuccess}
          isUploadError={isUploadError}
          uploadError={uploadError}
          rules={rules}
          onDrop={handleDrop}
          onDragOver={(e) => {
            e.preventDefault();
            setDragActive(true);
          }}
          onDragLeave={() => setDragActive(false)}
          onInputChange={(e) => {
            select(Array.from(e.target.files ?? []));
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

      <FilePreviewDialog file={previewing} onClose={() => setPreviewing(null)} />

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
  reindexing: boolean;
  onPreview: (file: WorkspaceFile) => void;
  onRetry: (id: string) => void;
  onDelete: (id: string, name: string) => void;
}

const FileRow = ({ file, reindexing, onPreview, onRetry, onDelete }: FileRowProps) => {
  const failed = file.processingStatus === "Failed";

  return (
    <li className={styles.fileRow}>
      <FileText size={16} className={styles.fileIcon} aria-hidden="true" />
      <div className={styles.fileInfo}>
        <span className={styles.fileName}>{file.fileName}</span>
        <span className={styles.fileMeta}>
          {formatBytesToMb(file.sizeBytes)} · {formatDateTimeShort(file.createdAt)}
        </span>
        {failed && file.processingError && <span className={styles.fileError}>{file.processingError}</span>}
      </div>
      <FileStatusBadge status={file.processingStatus} />
      {failed && (
        <Button
          variant="ghost"
          size="sm"
          iconOnly
          icon={<RotateCw size={15} />}
          title="Retry indexing"
          loading={reindexing}
          onClick={() => onRetry(file.id)}
        />
      )}
      <Button variant="ghost" size="sm" iconOnly icon={<Eye size={15} />} title="Preview" onClick={() => onPreview(file)} />
      <a
        className={styles.iconLink}
        href={workspaceFileContentUrl(file.id)}
        download={file.fileName}
        title="Download"
        aria-label={`Download ${file.fileName}`}
      >
        <Download size={15} />
      </a>
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
};
