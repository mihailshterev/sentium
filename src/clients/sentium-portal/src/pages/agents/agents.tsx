import { useState } from "react";
import { Bot, Plus, Loader } from "lucide-react";
import styles from "./agents.module.scss";
import useAgents from "../../hooks/useAgents";
import useModels from "../../hooks/useModels";
import type { AgentRecord } from "../../types/agents";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import AgentCard from "./components/agent-card";
import AgentCreateForm from "./components/agent-create-form";
import AgentEditModal from "./components/agent-edit-modal";

const Agents = () => {
  const {
    agents,
    isLoading,
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

  const handleDelete = (agentId: string) => {
    if (!confirm("Are you sure you want to delete this agent?")) {
      return;
    }
    deleteAgent(agentId, {
      onError: (err) => alert(err instanceof Error ? err.message : "Unknown error"),
    });
  };

  return (
    <div className={styles.agentsContainer}>
      <PageHeader
        icon={<Bot size={20} className={styles.headerIcon} />}
        title="Agent Registry"
        subtitle="Register and manage autonomous agents in the pipeline"
        right={
          <div className={styles.headerBadge}>
            <Bot size={14} />
            <span>{agents.length} registered</span>
          </div>
        }
      />

      <div className={styles.agentsBody}>
        <div className={styles.listPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.activeDot}></span>
            <span>Registered Agents</span>
            <span className={styles.agentCountBadge}>{agents.length}</span>
          </div>

          <div className={styles.agentList}>
            {isLoading && (
              <EmptyState icon={<Loader size={20} className={styles.spinIcon} />} title="Loading registry..." />
            )}
            {!isLoading && agents.length === 0 && (
              <EmptyState
                icon={<Bot size={32} />}
                title="No agents registered yet"
                hint="Register your first agent using the form."
              />
            )}
            {agents.map((agent, i) => (
              <AgentCard key={agent.id} agent={agent} index={i} onEdit={openEdit} onDelete={handleDelete} />
            ))}
          </div>
        </div>

        <div className={styles.panel}>
          <div className={styles.panelHeader}>
            <Plus size={14} />
            <span>Register Agent</span>
          </div>
          <AgentCreateForm
            models={models}
            isCreatingAgent={isCreatingAgent}
            isCreateSuccess={isCreateSuccess}
            isCreateError={isCreateError}
            createAgentError={createAgentError}
            onSubmit={handleCreateSubmit}
          />
        </div>
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
    </div>
  );
};

export default Agents;
