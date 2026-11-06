import { useMemo } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, GitBranch } from "lucide-react";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { useAgents } from "@/features/agents/hooks/useAgents";
import type { SortableAgentItem } from "@/features/workflows/types/workflows";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { WorkflowEditor } from "../components/workflow-editor";

export function WorkflowBuilderView() {
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

  const backButton = (
    <Button
      variant="secondary"
      size="sm"
      iconOnly
      icon={<ArrowLeft size={16} />}
      onClick={goBack}
      title="Back to workflows"
    />
  );

  const pageProps = {
    title: isCreate ? "New Workflow" : "Edit Workflow",
    subtitle: isCreate ? "Chain agents into an execution pipeline" : selectedWorkflow?.name,
    leading: backButton,
    width: "wide" as const,
  };

  if (!isCreate && (isLoadingAgents || !selectedWorkflow)) {
    return (
      <Page {...pageProps}>
        <Card padding="lg">
          {isLoading || isLoadingAgents ? (
            <div aria-busy="true">
              <span className="visually-hidden">Loading workflow...</span>
              <SkeletonText lines={4} />
            </div>
          ) : (
            <EmptyState
              icon={<GitBranch />}
              title="Workflow not found"
              hint="It may have been deleted."
              action={
                <Button variant="secondary" icon={<ArrowLeft size={15} />} onClick={goBack}>
                  Back to workflows
                </Button>
              }
            />
          )}
        </Card>
      </Page>
    );
  }

  return (
    <Page {...pageProps}>
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
    </Page>
  );
}
