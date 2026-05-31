import { useState } from "react";
import { useNavigate } from "react-router";
import { Plus, GitBranch, Loader } from "lucide-react";
import styles from "./workflows.module.scss";
import useWorkflows from "../../hooks/useWorkflows";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import WorkflowCard from "./components/workflow-card";
import ConfirmDialog from "../../components/ui/confirm-dialog";

const WorkflowsList = () => {
  const { workflows, isLoading, deleteWorkflow } = useWorkflows();
  const navigate = useNavigate();
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const confirmDelete = () => {
    if (!pendingDeleteId) {
      return;
    }
    deleteWorkflow(pendingDeleteId, { onSuccess: () => undefined });
    setPendingDeleteId(null);
  };

  return (
    <div className={styles.container}>
      <PageHeader
        icon={<GitBranch size={20} className={styles.headerIcon} />}
        title="Workflows"
        subtitle="Compose agents into ordered execution pipelines"
        right={
          <div className={styles.headerRight}>
            <div className={styles.headerBadge}>
              <GitBranch size={13} />
              <span>{workflows.length} workflows</span>
            </div>
            <button className={styles.createBtn} onClick={() => navigate("/workflows/new")}>
              <Plus size={14} />
              New Workflow
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
          <EmptyState icon={<Loader size={20} className={styles.spinIcon} />} title="Loading workflows..." />
        ) : workflows.length === 0 ? (
          <EmptyState
            icon={<GitBranch size={32} />}
            title="No workflows yet"
            hint="Create your first workflow to chain agents into a pipeline"
            action={
              <button className={styles.createBtn} onClick={() => navigate("/workflows/new")}>
                <Plus size={14} />
                New Workflow
              </button>
            }
          />
        ) : (
          <div className={styles.gridScroll}>
            <div className={styles.workflowGrid}>
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
          </div>
        )}
      </div>

      <ConfirmDialog
        open={pendingDeleteId !== null}
        variant="danger"
        title="Delete workflow?"
        description="This will permanently remove the workflow and its pipeline configuration. This action cannot be undone."
        confirmLabel="Delete workflow"
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
      />
    </div>
  );
};

export default WorkflowsList;
