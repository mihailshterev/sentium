import { useState } from "react";
import { useNavigate } from "react-router";
import { GitBranch, Plus } from "lucide-react";
import styles from "../workflows.module.css";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { WorkflowCard } from "../components/workflow-card";

export function WorkflowsView() {
  const { workflows, totalCount, isLoading, hasMore, loadMore, isLoadingMore, deleteWorkflow } = useWorkflows();
  const navigate = useNavigate();
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const confirmDelete = () => {
    if (!pendingDeleteId) {
      return;
    }
    deleteWorkflow(pendingDeleteId, { onSuccess: () => undefined });
    setPendingDeleteId(null);
  };

  const newWorkflowButton = (
    <Button variant="primary" icon={<Plus size={16} />} onClick={() => navigate("/workflows/new")}>
      New Workflow
    </Button>
  );

  return (
    <Page
      title="Workflows"
      subtitle="Compose agents into ordered execution pipelines"
      actions={
        <>
          <Badge tone="neutral">{totalCount} workflows</Badge>
          {newWorkflowButton}
        </>
      }
    >
      {isLoading ? (
        <div className={styles.grid} aria-busy="true">
          <span className="visually-hidden">Loading workflows...</span>
          {[0, 1, 2].map((i) => (
            <Card key={i} padding="md" className={styles.skeletonCard}>
              <Skeleton width="45%" height={14} />
              <Skeleton width="85%" />
              <Skeleton width="30%" />
            </Card>
          ))}
        </div>
      ) : workflows.length === 0 ? (
        <Card>
          <EmptyState
            icon={<GitBranch />}
            title="No workflows yet"
            hint="Create your first workflow to chain agents into a pipeline"
            action={newWorkflowButton}
          />
        </Card>
      ) : (
        <>
          <div className={styles.grid}>
            {workflows.map((wf) => (
              <WorkflowCard
                key={wf.id}
                workflow={wf}
                isActive={false}
                onSelect={(w) => navigate(`/workflows/${w.id}`)}
                onEdit={(w) => navigate(`/workflows/${w.id}`)}
                onDelete={setPendingDeleteId}
              />
            ))}
          </div>
          <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
        </>
      )}

      <ConfirmDialog
        open={pendingDeleteId !== null}
        variant="danger"
        title="Delete workflow?"
        description="This will permanently remove the workflow and its pipeline configuration. This action cannot be undone."
        confirmLabel="Delete workflow"
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
      />
    </Page>
  );
}
