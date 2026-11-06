import { useState } from "react";
import { Bot, Plus } from "lucide-react";
import styles from "../agents.module.css";
import { useAgents } from "@/features/agents/hooks/useAgents";
import { useModels } from "@/features/models/hooks/useModels";
import type { AgentRecord } from "@/features/agents/types/agents";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { AgentCard } from "../components/agent-card";
import { AgentCreateForm } from "../components/agent-create-form";
import { AgentEditModal } from "../components/agent-edit-modal";

const LoadingRows = () => (
  <ul className={styles.agentList} aria-busy="true">
    {[0, 1, 2].map((i) => (
      <li key={i} className={styles.skeletonRow}>
        <Skeleton width={36} height={36} radius="md" />
        <span className={styles.skeletonText}>
          <Skeleton width="30%" />
          <Skeleton width="70%" />
        </span>
      </li>
    ))}
  </ul>
);

export function AgentsView() {
  const {
    agents,
    totalCount,
    isLoading,
    hasMore,
    loadMore,
    isLoadingMore,
    createAgent,
    isCreatingAgent,
    isCreateSuccess,
    isCreateError,
    createAgentError,
    resetCreate,
    updateAgent,
    isUpdatingAgent,
    isUpdateSuccess,
    isUpdateError,
    updateAgentError,
    resetUpdate,
    deleteAgent,
  } = useAgents();

  const { models } = useModels();

  const [editAgent, setEditAgent] = useState<AgentRecord | null>(null);
  const [confirmState, setConfirmState] = useState<{ agentId: string } | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const handleCreateSubmit = (data: { name: string; description: string; model: string }) => {
    createAgent(data, {
      onSuccess: () => setTimeout(() => resetCreate(), 2500),
    });
  };

  const openEdit = (agent: AgentRecord) => {
    setEditAgent(agent);
    resetUpdate();
  };

  const closeEdit = () => {
    setEditAgent(null);
    resetUpdate();
  };

  const handleEditSubmit = (data: { id: string; name: string; description: string; model: string }) => {
    updateAgent(data, {
      onSuccess: () => setTimeout(() => closeEdit(), 1000),
    });
  };

  return (
    <Page
      title="Agent Registry"
      subtitle="Register and manage autonomous agents"
      actions={<Badge tone="neutral">{totalCount} registered</Badge>}
    >
      <div className={styles.layout}>
        <Card padding="none" as="section" className={styles.listCard}>
          <CardHeader title="Registered agents" description="Agents available to workflows and the assistant" />

          {deleteError && (
            <div className={styles.inlineMessage}>
              <StatusMessage variant="error" message={deleteError} />
            </div>
          )}

          {isLoading ? (
            <>
              <span className="visually-hidden">Loading registry...</span>
              <LoadingRows />
            </>
          ) : agents.length === 0 ? (
            <EmptyState
              icon={<Bot />}
              title="No agents registered yet"
              hint="Register your first agent using the form."
            />
          ) : (
            <ul className={styles.agentList}>
              {agents.map((agent) => (
                <AgentCard
                  key={agent.id}
                  agent={agent}
                  onEdit={openEdit}
                  onDelete={(agentId) => setConfirmState({ agentId })}
                />
              ))}
            </ul>
          )}
          <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
        </Card>

        <Card padding="md" as="section" className={styles.formCard}>
          <CardHeader icon={<Plus />} title="Register agent" description="Name it, describe its role, pick a model" />
          <AgentCreateForm
            models={models}
            isCreatingAgent={isCreatingAgent}
            isCreateSuccess={isCreateSuccess}
            isCreateError={isCreateError}
            createAgentError={createAgentError}
            onSubmit={handleCreateSubmit}
          />
        </Card>
      </div>

      {editAgent && (
        <AgentEditModal
          agent={editAgent}
          models={models}
          isUpdatingAgent={isUpdatingAgent}
          isUpdateSuccess={isUpdateSuccess}
          isUpdateError={isUpdateError}
          updateAgentError={updateAgentError}
          onSubmit={handleEditSubmit}
          onClose={closeEdit}
        />
      )}

      {confirmState && (
        <ConfirmDialog
          open
          variant="danger"
          title="Delete agent?"
          description="This will permanently remove the agent from the registry. This action cannot be undone."
          confirmLabel="Delete agent"
          onConfirm={() => {
            setDeleteError(null);
            deleteAgent(confirmState.agentId, {
              onError: (err) => setDeleteError(err instanceof Error ? err.message : "Failed to delete agent."),
            });
            setConfirmState(null);
          }}
          onCancel={() => setConfirmState(null)}
        />
      )}
    </Page>
  );
}
