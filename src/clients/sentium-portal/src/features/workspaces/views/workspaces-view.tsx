import { useState } from "react";
import { useNavigate } from "react-router";
import { FolderOpen, Plus } from "lucide-react";
import styles from "../workspaces.module.css";
import type { Workspace } from "@/features/workspaces/types/workspace";
import { useWorkspaces } from "@/features/workspaces/hooks/useWorkspaces";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { WorkspaceCard } from "../components/workspace-card";
import { WorkspaceForm } from "../components/workspace-form";

export function WorkspacesView() {
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

  const newWorkspaceButton = (
    <Button variant="primary" icon={<Plus size={16} />} onClick={() => setShowCreateForm(true)}>
      New Workspace
    </Button>
  );

  return (
    <Page
      title="Workspaces"
      subtitle="Organize files for agent access, RAG indexing, and analysis"
      actions={
        <>
          <Badge tone="neutral">
            {totalCount} workspace{totalCount !== 1 ? "s" : ""}
          </Badge>
          {newWorkspaceButton}
        </>
      }
    >
      {isLoading ? (
        <div className={styles.grid} aria-busy="true">
          <span className="visually-hidden">Loading workspaces...</span>
          {[0, 1, 2].map((i) => (
            <Card key={i} padding="md" className={styles.skeletonCard}>
              <Skeleton width={36} height={36} radius="md" />
              <Skeleton width="50%" height={14} />
              <Skeleton width="80%" />
            </Card>
          ))}
        </div>
      ) : isError ? (
        <Card>
          <EmptyState icon={<FolderOpen />} title="Failed to load workspaces" hint="Please try refreshing the page" />
        </Card>
      ) : workspaces.length === 0 ? (
        <Card>
          <EmptyState
            icon={<FolderOpen />}
            title="No workspaces yet"
            hint="Create your first workspace to organize files for agent access and RAG indexing"
            action={newWorkspaceButton}
          />
        </Card>
      ) : (
        <>
          <div className={styles.grid}>
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
        </>
      )}

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
    </Page>
  );
}
