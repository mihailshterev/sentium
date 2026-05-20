import { useRef, useState, useCallback } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { FolderOpen, Plus } from "lucide-react";
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
import PageHeader from "../../components/ui/page-header";
import WorkspaceForm from "./components/workspace-form";
import WorkspaceFileCard from "./components/workspace-file-card";
import UploadPanel from "./components/upload-panel";
import WorkspaceSidebar from "./components/workspace-sidebar";

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

const hasPending = (files: WorkspaceFile[]) =>
  files.some((f) => f.processingStatus === "Pending" || f.processingStatus === "Processing");

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
      <PageHeader
        icon={<FolderOpen size={20} className={styles.titleIcon} />}
        title="Workspaces"
        subtitle="Organize files for agent access, RAG indexing, and analysis"
        right={
          <button className={styles.newButton} onClick={() => setShowCreateForm(true)}>
            <Plus size={14} />
            New Workspace
          </button>
        }
      />

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
                      <WorkspaceFileCard key={f.id} file={f} onDelete={(id) => deleteFile(id)} />
                    ))}
                  </div>
                </div>

                <UploadPanel
                  workspaceName={selectedWorkspace.name}
                  selectedFile={selectedFile}
                  dragActive={dragActive}
                  fileTypeError={fileTypeError}
                  isUploading={isUploading}
                  isUploadSuccess={isUploadSuccess}
                  isUploadError={isUploadError}
                  uploadError={uploadError instanceof Error ? uploadError : null}
                  acceptedExtensions={ALLOWED_EXTENSIONS.join(",")}
                  onDrop={handleDrop}
                  onDragOver={handleDragOver}
                  onDragLeave={handleDragLeave}
                  onInputChange={handleInputChange}
                  onSubmit={handleUploadSubmit}
                  fileInputRef={fileInputRef}
                />
              </div>
            </>
          )}
        </main>

        <WorkspaceSidebar
          workspaces={workspaces}
          selectedWorkspace={selectedWorkspace}
          isWorkspacesError={isWorkspacesError}
          onSelect={setSelectedWorkspace}
          onEdit={setEditingWorkspace}
          onDelete={(id) => deleteWs(id)}
          onCreateNew={() => setShowCreateForm(true)}
        />
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
