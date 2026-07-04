import { useMemo } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, GitBranch, Loader } from "lucide-react";
import styles from "./workflows.module.scss";
import useWorkflows from "../../hooks/useWorkflows";
import useAgents from "../../hooks/useAgents";
import type { SortableAgentItem } from "../../types/workflows";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import WorkflowEditor from "./components/workflow-editor";

const WorkflowBuilder = () => {
  const { workflowId } = useParams<{ workflowId?: string }>();
  const navigate = useNavigate();
  const isCreate = !workflowId;

  const {
    workflows,
    isLoading,
    createWorkflow,
    isCreatingWorkflow,
    isCreateSuccess,
    isCreateError,
    createWorkflowError,
    updateWorkflow,
    isUpdatingWorkflow,
    isUpdateSuccess,
    isUpdateError,
    updateWorkflowError,
  } = useWorkflows();
  const { agents, isLoading: isLoadingAgents } = useAgents();

  const selectedWorkflow = workflowId ? (workflows.find((w) => w.id === workflowId) ?? null) : null;

  const initialFormAgents = useMemo<SortableAgentItem[]>(() => {
    if (!selectedWorkflow) {
      return [];
    }
    return selectedWorkflow.agents.map((a) => {
      const ag = agents.find((agent) => agent.id === a.agentId);
      return {
        sortId: `${a.agentId}-${a.order}`,
        agentId: a.agentId,
        name: ag?.name ?? a.agentId.slice(0, 8),
        model: ag?.model ?? "",
        description: ag?.description ?? "",
      };
    });
  }, [selectedWorkflow, agents]);

  const goBack = () => navigate("/workflows");

  const handleSubmit = (data: { name: string; description: string; agents: { agentId: string; order: number }[] }) => {
    if (selectedWorkflow) {
      updateWorkflow({ id: selectedWorkflow.id, ...data }, { onSuccess: () => setTimeout(goBack, 700) });
    } else {
      createWorkflow(data, { onSuccess: () => setTimeout(goBack, 700) });
    }
  };

  const getAgentName = (agentId: string) => agents.find((a) => a.id === agentId)?.name ?? agentId.slice(0, 8);

  const header = (
    <PageHeader
      icon={
        <button className={styles.backBtn} onClick={goBack} title="Back to workflows">
          <ArrowLeft size={16} />
        </button>
      }
      title={isCreate ? "New Workflow" : "Edit Workflow"}
      subtitle={isCreate ? "Chain agents into an execution pipeline" : selectedWorkflow?.name}
    />
  );

  if (!isCreate && (isLoadingAgents || !selectedWorkflow)) {
    return (
      <div className={styles.container}>
        {header}
        <div className={styles.builderBody}>
          {isLoading || isLoadingAgents ? (
            <EmptyState icon={<Loader size={20} className={styles.spinIcon} />} title="Loading workflow..." />
          ) : (
            <EmptyState
              icon={<GitBranch size={32} />}
              title="Workflow not found"
              hint="It may have been deleted."
              action={
                <button className={styles.createBtn} onClick={goBack}>
                  <ArrowLeft size={14} />
                  Back to workflows
                </button>
              }
            />
          )}
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {header}
      <div className={styles.builderBody}>
        <div className={styles.pipelineBg} aria-hidden="true">
          <div className={styles.bgGrid} />
          <div className={styles.bgOrb1} />
          <div className={styles.bgOrb2} />
          <div className={styles.bgOrb3} />
        </div>
        <WorkflowEditor
          key={workflowId ?? "new"}
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
          onSubmit={handleSubmit}
          getAgentName={getAgentName}
          initialFormAgents={initialFormAgents}
          initialName={selectedWorkflow?.name ?? ""}
          initialDescription={selectedWorkflow?.description ?? ""}
        />
      </div>
    </div>
  );
};

export default WorkflowBuilder;
