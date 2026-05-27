import { useState } from "react";
import { Plus, GitBranch, Loader } from "lucide-react";
import styles from "./workflows.module.scss";
import useWorkflows from "../../hooks/useWorkflows";
import useAgents from "../../hooks/useAgents";
import type { WorkflowRecord, SortableAgentItem } from "../../types/workflows";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import WorkflowCard from "./components/workflow-card";
import WorkflowEditor from "./components/workflow-editor";
import ConfirmDialog from "../../components/ui/confirm-dialog";

const Workflows = () => {
  const {
    workflows,
    isLoading,
    createWorkflow,
    isCreatingWorkflow,
    isCreateSuccess,
    isCreateError,
    createWorkflowError,
    resetCreate,
    updateWorkflow,
    isUpdatingWorkflow,
    isUpdateSuccess,
    isUpdateError,
    updateWorkflowError,
    resetUpdate,
    deleteWorkflow,
  } = useWorkflows();
  const { agents } = useAgents();

  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [editorKey, setEditorKey] = useState(0);
  const [initialName, setInitialName] = useState("");
  const [initialDescription, setInitialDescription] = useState("");
  const [initialFormAgents, setInitialFormAgents] = useState<SortableAgentItem[]>([]);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const resetMutations = () => {
    resetCreate();
    resetUpdate();
  };

  const openCreate = () => {
    setSelectedWorkflow(null);
    setInitialName("");
    setInitialDescription("");
    setInitialFormAgents([]);
    setIsEditing(true);
    setEditorKey((k) => k + 1);
    resetMutations();
  };

  const openEdit = (workflow: WorkflowRecord) => {
    setSelectedWorkflow(workflow);
    setInitialName(workflow.name);
    setInitialDescription(workflow.description);
    setInitialFormAgents(
      workflow.agents.map((a) => {
        const ag = agents.find((ag) => ag.id === a.agentId);
        return {
          sortId: `${a.agentId}-${a.order}`,
          agentId: a.agentId,
          name: ag?.name ?? a.agentId.slice(0, 8),
          model: ag?.model ?? "",
          description: ag?.description ?? "",
        };
      }),
    );
    setIsEditing(true);
    setEditorKey((k) => k + 1);
    resetMutations();
  };

  const closeEdit = () => {
    setIsEditing(false);
    setSelectedWorkflow(null);
    resetMutations();
  };

  const handleSubmit = (data: { name: string; description: string; agents: { agentId: string; order: number }[] }) => {
    if (selectedWorkflow) {
      updateWorkflow({ id: selectedWorkflow.id, ...data }, { onSuccess: () => setTimeout(() => resetUpdate(), 900) });
    } else {
      createWorkflow(data, {
        onSuccess: () => setTimeout(() => closeEdit(), 900),
      });
    }
  };

  const handleDelete = (workflowId: string) => {
    setPendingDeleteId(workflowId);
  };

  const confirmDelete = () => {
    if (!pendingDeleteId) {
      return;
    }
    deleteWorkflow(pendingDeleteId, {
      onSuccess: () => {
        if (selectedWorkflow?.id === pendingDeleteId) {
          closeEdit();
        }
      },
    });
    setPendingDeleteId(null);
  };

  const getAgentName = (agentId: string) => agents.find((a) => a.id === agentId)?.name ?? agentId.slice(0, 8);

  return (
    <div className={styles.container}>
      <PageHeader
        icon={<GitBranch size={20} className={styles.headerIcon} />}
        title="Workflow Builder"
        subtitle="Compose agents into ordered execution pipelines"
        right={
          <div className={styles.headerRight}>
            <div className={styles.headerBadge}>
              <GitBranch size={13} />
              <span>{workflows.length} workflows</span>
            </div>
            <button className={styles.createBtn} onClick={openCreate}>
              <Plus size={14} />
              New Workflow
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <main className={styles.editorPanel}>
          <div className={styles.pipelineBg} aria-hidden="true">
            <div className={styles.bgGrid} />
            <div className={styles.bgOrb1} />
            <div className={styles.bgOrb2} />
            <div className={styles.bgOrb3} />
          </div>
          {!isEditing ? (
            <div className={styles.editorEmpty}>
              <GitBranch size={36} className={styles.editorEmptyIcon} />
              <p>Select a workflow to edit or create a new one</p>
              <button className={styles.createBtn} onClick={openCreate}>
                <Plus size={14} />
                New Workflow
              </button>
            </div>
          ) : (
            <WorkflowEditor
              key={editorKey}
              selectedWorkflow={selectedWorkflow}
              agents={agents}
              isCreatingWorkflow={isCreatingWorkflow}
              isUpdatingWorkflow={isUpdatingWorkflow}
              isCreateSuccess={isCreateSuccess}
              isUpdateSuccess={isUpdateSuccess}
              isCreateError={isCreateError}
              isUpdateError={isUpdateError}
              createWorkflowError={createWorkflowError}
              updateWorkflowError={updateWorkflowError}
              onClose={closeEdit}
              onSubmit={handleSubmit}
              getAgentName={getAgentName}
              initialFormAgents={initialFormAgents}
              initialName={initialName}
              initialDescription={initialDescription}
            />
          )}
        </main>

        <aside className={styles.workflowList}>
          <div className={styles.listHeader}>
            <span className={styles.activeDot} />
            <span>Defined Workflows</span>
          </div>

          <div className={styles.listScroll}>
            {isLoading && <EmptyState icon={<Loader size={18} className={styles.spinIcon} />} title="Loading..." />}
            {!isLoading && workflows.length === 0 && (
              <EmptyState icon={<GitBranch size={28} />} title="No workflows yet" />
            )}
            {workflows.map((wf) => (
              <WorkflowCard
                key={wf.id}
                workflow={wf}
                isActive={selectedWorkflow?.id === wf.id}
                onSelect={openEdit}
                onEdit={openEdit}
                onDelete={handleDelete}
              />
            ))}
          </div>
        </aside>
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

export default Workflows;
