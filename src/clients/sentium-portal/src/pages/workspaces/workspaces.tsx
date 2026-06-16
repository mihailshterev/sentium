import { useState } from "react";
import { useNavigate } from "react-router";
import { FolderOpen, Plus, Loader } from "lucide-react";
import styles from "./workspaces.module.scss";
import type { Workspace } from "../../types/workspace";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import LoadMore from "../../components/ui/load-more";
import WorkspaceForm from "./components/workspace-form";
import WorkspaceCard from "./components/workspace-card";
import ConfirmDialog from "../../components/ui/confirm-dialog";
import useWorkspaces from "../../hooks/useWorkspaces";

const WorkspacesList = () => {
  const navigate = useNavigate();
  const {
    workspaces,
    totalCount,
    isLoading,
    isError,
    hasMore,
    loadMore,
    isLoadingMore,
    createWorkspace,
    isCreatingWorkspace,
    updateWorkspace,
    isUpdatingWorkspace,
    deleteWorkspace,
  } = useWorkspaces();

  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingWorkspace, setEditingWorkspace] = useState<Workspace | null>(null);
  const [pendingDelete, setPendingDelete] = useState<{ id: string; name: string } | null>(null);
  const [createError, setCreateError] = useState<string | null>(null);
  const [updateError, setUpdateError] = useState<string | null>(null);

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<FolderOpen size={20} className={styles.headerIcon} />}
        title="Workspaces"
        subtitle="Organize files for agent access, RAG indexing, and analysis"
        right={
          <div className={styles.headerRight}>
            <div className={styles.headerBadge}>
              <FolderOpen size={13} />
              <span>
                {totalCount} workspace{totalCount !== 1 ? "s" : ""}
              </span>
            </div>
            <button className={styles.createBtn} onClick={() => setShowCreateForm(true)}>
              <Plus size={14} />
              New Workspace
            </button>
          </div>
        }
      />

      <div className={styles.listBody}>
        <div className={styles.pipelineBg} aria-hidden="true">
          <div className={styles.bgGrid} />
          <div className={styles.bgOrb1} />
          <div className={styles.bgOrb2} />
          <div className={styles.bgOrb3} />
        </div>

        {isLoading ? (
          <EmptyState icon={<Loader size={20} className={styles.spinIcon} />} title="Loading workspaces..." />
        ) : isError ? (
          <EmptyState
            icon={<FolderOpen size={32} />}
            title="Failed to load workspaces"
            hint="Please try refreshing the page"
          />
        ) : workspaces.length === 0 ? (
          <EmptyState
            icon={<FolderOpen size={36} />}
            title="No workspaces yet"
            hint="Create your first workspace to organize files for agent access and RAG indexing"
            action={
              <button className={styles.createBtn} onClick={() => setShowCreateForm(true)}>
                <Plus size={14} />
                New Workspace
              </button>
            }
          />
        ) : (
          <div className={styles.gridScroll}>
            <div className={styles.workspaceGrid}>
              {workspaces.map((ws) => (
                <WorkspaceCard
                  key={ws.id}
                  workspace={ws}
                  onOpen={() => navigate(`/workspaces/${ws.id}`)}
                  onEdit={(e) => {
                    e.stopPropagation();
                    setEditingWorkspace(ws);
                  }}
                  onDelete={(e) => {
                    e.stopPropagation();
                    setPendingDelete({ id: ws.id, name: ws.name });
                  }}
                />
              ))}
            </div>
            <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
          </div>
        )}
      </div>

      {showCreateForm && (
        <WorkspaceForm
          title="Create Workspace"
          isPending={isCreatingWorkspace}
          error={createError}
          onSubmit={(name, description) => {
            setCreateError(null);
            createWorkspace(
              { name, description: description || undefined },
              {
                onSuccess: (created) => {
                  setShowCreateForm(false);
                  navigate(`/workspaces/${created.id}`);
                },
                onError: (err) => setCreateError(err instanceof Error ? err.message : "Failed to create workspace"),
              },
            );
          }}
          onCancel={() => {
            setShowCreateForm(false);
            setCreateError(null);
          }}
        />
      )}

      {editingWorkspace && (
        <WorkspaceForm
          title="Edit Workspace"
          initial={{ name: editingWorkspace.name, description: editingWorkspace.description ?? "" }}
          isPending={isUpdatingWorkspace}
          error={updateError}
          onSubmit={(name, description) => {
            setUpdateError(null);
            updateWorkspace(
              { id: editingWorkspace.id, name, description: description || undefined },
              {
                onSuccess: () => {
                  setEditingWorkspace(null);
                  setUpdateError(null);
                },
                onError: (err) => setUpdateError(err instanceof Error ? err.message : "Failed to update workspace"),
              },
            );
          }}
          onCancel={() => {
            setEditingWorkspace(null);
            setUpdateError(null);
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        variant="danger"
        title="Delete Workspace"
        description={`Are you sure you want to delete "${pendingDelete?.name}"? All files will be permanently disassociated.`}
        confirmLabel="Delete workspace"
        onConfirm={() =>
          pendingDelete &&
          deleteWorkspace(pendingDelete.id, {
            onSuccess: () => setPendingDelete(null),
            onError: (err) => {
              setPendingDelete(null);
              alert(err instanceof Error ? err.message : "Failed to delete workspace");
            },
          })
        }
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
};

export default WorkspacesList;
