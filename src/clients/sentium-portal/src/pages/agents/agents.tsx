import { useState } from "react";
import { Bot, Plus, CheckCircle, AlertCircle, Loader, Clock, Pencil, X, Cpu } from "lucide-react";
import styles from "./agents.module.scss";
import useAgents from "../../hooks/useAgents";
import useModels from "../../hooks/useModels";
import type { AgentRecord } from "../../types/agents";

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

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [model, setModel] = useState("");

  const [editAgent, setEditAgent] = useState<AgentRecord | null>(null);
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editModel, setEditModel] = useState("");

  const selectedModel = model || models[0] || "";

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createAgent(
      {
        name: name.trim(),
        description: description.trim(),
        model: selectedModel.trim(),
      },
      {
        onSuccess: () => {
          setName("");
          setDescription("");
          setModel("");
          setTimeout(() => resetCreate(), 2500);
        },
      },
    );
  };

  const openEdit = (agent: AgentRecord) => {
    setEditAgent(agent);
    setEditName(agent.name);
    setEditDescription(agent.description);
    setEditModel(agent.model || models[0] || "");
    resetUpdate();
  };

  const closeEdit = () => {
    setEditAgent(null);
    resetUpdate();
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editAgent) return;
    updateAgent(
      {
        id: editAgent.id,
        name: editName.trim(),
        description: editDescription.trim(),
        model: editModel.trim(),
      },
      {
        onSuccess: () => setTimeout(() => closeEdit(), 1000),
      },
    );
  };

  const handleDelete = (agentId: string) => {
    if (!confirm("Are you sure you want to delete this agent?")) return;
    deleteAgent(agentId, {
      onError: (err) => alert(err instanceof Error ? err.message : "Unknown error"),
    });
  };

  return (
    <div className={styles.agentsContainer}>
      <div className={styles.agentsHeader}>
        <div className={styles.headerLeft}>
          <Bot size={16} className={styles.headerIcon} />
          <div>
            <h2 className={styles.headerTitle}>Agent Registry</h2>
            <span className={styles.headerSub}>Register and manage autonomous agents in the pipeline</span>
          </div>
        </div>
        <div className={styles.headerBadge}>
          <Bot size={14} />
          <span>{agents.length} registered</span>
        </div>
      </div>

      <div className={styles.agentsBody}>
        <div className={styles.panel}>
          <div className={styles.panelHeader}>
            <Plus size={14} />
            <span>Register Agent</span>
          </div>

          <form className={styles.createForm} onSubmit={handleSubmit}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="agent-name">
                Agent Name
              </label>
              <input
                id="agent-name"
                className={styles.fieldInput}
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. ForensicsAgent"
                maxLength={255}
                required
                autoComplete="off"
                spellCheck={false}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="agent-description">
                Description
                <span className={styles.charCount}>{description.length}/1000</span>
              </label>
              <textarea
                id="agent-description"
                className={`${styles.fieldInput} ${styles.fieldTextarea}`}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe this agent's capabilities and role in the pipeline..."
                maxLength={1000}
                rows={4}
                spellCheck={false}
              />
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="agent-model">
                <Cpu size={12} />
                Model
              </label>
              {models.length > 0 ? (
                <select
                  id="agent-model"
                  className={styles.fieldInput}
                  value={selectedModel}
                  onChange={(e) => setModel(e.target.value)}
                >
                  {models.map((m) => (
                    <option key={m} value={m}>
                      {m}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  id="agent-model"
                  className={styles.fieldInput}
                  type="text"
                  value={selectedModel}
                  onChange={(e) => setModel(e.target.value)}
                  placeholder="e.g. gemma3:1b"
                  autoComplete="off"
                  spellCheck={false}
                />
              )}
            </div>

            <button
              className={`${styles.submitBtn} ${isCreatingAgent ? styles.submitting : ""}`}
              type="submit"
              disabled={isCreatingAgent || !name.trim()}
            >
              {isCreatingAgent ? (
                <>
                  <Loader size={14} className={styles.spinIcon} />
                  Registering...
                </>
              ) : (
                <>
                  <Plus size={14} />
                  Register Agent
                </>
              )}
            </button>

            {isCreateSuccess && (
              <div className={`${styles.statusMsg} ${styles.success}`}>
                <CheckCircle size={14} />
                Agent registered successfully
              </div>
            )}
            {isCreateError && (
              <div className={`${styles.statusMsg} ${styles.error}`}>
                <AlertCircle size={14} />
                {createAgentError?.message ?? "Unknown error"}
              </div>
            )}
          </form>
        </div>

        <div className={styles.listPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.activeDot}></span>
            <span>Registered Agents</span>
            <span className={styles.agentCountBadge}>{agents.length}</span>
          </div>

          <div className={styles.agentList}>
            {isLoading && (
              <div className={styles.listPlaceholder}>
                <Loader size={20} className={styles.spinIcon} />
                <span>Loading registry...</span>
              </div>
            )}
            {!isLoading && agents.length === 0 && (
              <div className={styles.listPlaceholder}>
                <Bot size={32} className={styles.emptyIcon} />
                <span>No agents registered yet</span>
                <span className={styles.emptyHint}>Register your first agent using the form.</span>
              </div>
            )}
            {agents.map((agent, i) => (
              <div className={styles.agentCard} key={agent.id}>
                <div className={styles.agentCardLeft}>
                  <div className={styles.agentAvatar}>
                    <Bot size={16} />
                  </div>
                </div>
                <div className={styles.agentCardContent}>
                  <div className={styles.agentCardHeader}>
                    <span className={styles.agentName}>{agent.name}</span>
                    <span className={styles.agentIndex}>#{String(i + 1).padStart(2, "0")}</span>
                    <span className={styles.agentStatusBadge}>Active</span>
                  </div>
                  <p className={styles.agentDescription}>{agent.description || "No description provided."}</p>
                  <div className={styles.agentMeta}>
                    {agent.model && (
                      <span className={styles.agentMetaItem}>
                        <Cpu size={11} />
                        {agent.model}
                      </span>
                    )}
                    <span className={styles.agentMetaItem}>
                      <Clock size={11} />
                      {new Date(agent.createdAt).toLocaleDateString("en-GB", {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                      })}
                    </span>
                    <span className={styles.agentMetaItem}>ID: {agent.id.slice(0, 8)}</span>
                  </div>
                </div>
                <div className={styles.agentCardActions}>
                  <button className={styles.iconBtn} onClick={() => openEdit(agent)} title="Edit agent">
                    <Pencil size={13} />
                  </button>
                  <button
                    className={`${styles.iconBtn} ${styles.iconBtnDanger}`}
                    onClick={() => handleDelete(agent.id)}
                    title="Delete agent"
                  >
                    <X size={13} />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {editAgent && (
        <div className={styles.modalOverlay} onClick={closeEdit}>
          <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
            <div className={styles.modalHeader}>
              <span>Edit Agent</span>
              <button className={styles.modalClose} onClick={closeEdit}>
                <X size={14} />
              </button>
            </div>
            <form onSubmit={handleEditSubmit} className={styles.createForm}>
              <div className={styles.fieldGroup}>
                <label className={styles.fieldLabel} htmlFor="edit-name">
                  Agent Name
                </label>
                <input
                  id="edit-name"
                  className={styles.fieldInput}
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  maxLength={255}
                  required
                  autoComplete="off"
                  spellCheck={false}
                />
              </div>

              <div className={styles.fieldGroup}>
                <label className={styles.fieldLabel} htmlFor="edit-description">
                  Description
                  <span className={styles.charCount}>{editDescription.length}/1000</span>
                </label>
                <textarea
                  id="edit-description"
                  className={`${styles.fieldInput} ${styles.fieldTextarea}`}
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  maxLength={1000}
                  rows={4}
                  spellCheck={false}
                />
              </div>

              <div className={styles.fieldGroup}>
                <label className={styles.fieldLabel} htmlFor="edit-model">
                  <Cpu size={12} />
                  Model
                </label>
                {models.length > 0 ? (
                  <select
                    id="edit-model"
                    className={styles.fieldInput}
                    value={editModel}
                    onChange={(e) => setEditModel(e.target.value)}
                  >
                    {models.map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id="edit-model"
                    className={styles.fieldInput}
                    type="text"
                    value={editModel}
                    onChange={(e) => setEditModel(e.target.value)}
                    placeholder="e.g. gemma3:1b"
                    autoComplete="off"
                    spellCheck={false}
                  />
                )}
              </div>

              <button
                className={`${styles.submitBtn} ${isUpdatingAgent ? styles.submitting : ""}`}
                type="submit"
                disabled={isUpdatingAgent || !editName.trim()}
              >
                {isUpdatingAgent ? (
                  <>
                    <Loader size={14} className={styles.spinIcon} /> Saving...
                  </>
                ) : isUpdateSuccess ? (
                  <>
                    <CheckCircle size={14} /> Saved
                  </>
                ) : (
                  "Save Changes"
                )}
              </button>

              {isUpdateError && (
                <div className={`${styles.statusMsg} ${styles.error}`}>
                  <AlertCircle size={14} />
                  {updateAgentError?.message ?? "Unknown error"}
                </div>
              )}
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default Agents;
