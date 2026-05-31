import { useState } from "react";
import { useNavigate } from "react-router";
import { FolderOpen, Plus, Loader } from "lucide-react";
import styles from "./workspaces.module.scss";
import type { Workspace } from "../../types/workspace";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import WorkspaceForm from "./components/workspace-form";
import WorkspaceCard from "./components/workspace-card";
import ConfirmDialog from "../../components/ui/confirm-dialog";
import useWorkspaces from "../../hooks/useWorkspaces";

const WorkspacesList = () => {
  const navigate = useNavigate();
  const {
    workspaces,
    isLoading,
    isError,
    createWorkspace,
    isCreatingWorkspace,
    updateWorkspace,
    isUpdatingWorkspace,
    deleteWorkspace,
  } = useWorkspaces();

  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingWorkspace, setEditingWorkspace] = useState<Workspace | null>(null);
  const [pendingDelete, setPendingDelete] = useState<{ id: string; name: string } | null>(null);

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
                {workspaces.length} workspace{workspaces.length !== 1 ? "s" : ""}
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
          </div>
        )}
      </div>

      {showCreateForm && (
        <WorkspaceForm
          title="Create Workspace"
          isPending={isCreatingWorkspace}
          onSubmit={(name, description) =>
            createWorkspace(
              { name, description: description || undefined },
              {
                onSuccess: (created) => {
                  setShowCreateForm(false);
                  navigate(`/workspaces/${created.id}`);
                },
              },
            )
          }
          onCancel={() => setShowCreateForm(false)}
        />
      )}

      {editingWorkspace && (
        <WorkspaceForm
          title="Edit Workspace"
          initial={{ name: editingWorkspace.name, description: editingWorkspace.description ?? "" }}
          isPending={isUpdatingWorkspace}
          onSubmit={(name, description) =>
            updateWorkspace(
              { id: editingWorkspace.id, name, description: description || undefined },
              { onSuccess: () => setEditingWorkspace(null) },
            )
          }
          onCancel={() => setEditingWorkspace(null)}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        variant="danger"
        title="Delete Workspace"
        description={`Are you sure you want to delete "${pendingDelete?.name}"? All files will be permanently disassociated.`}
        confirmLabel="Delete workspace"
        onConfirm={() =>
          pendingDelete && deleteWorkspace(pendingDelete.id, { onSuccess: () => setPendingDelete(null) })
        }
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  );
};

export default WorkspacesList;
