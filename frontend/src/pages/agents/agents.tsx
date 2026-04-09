import { useState, useEffect } from "react";
import { Bot, Plus, CheckCircle, AlertCircle, Loader, Clock, Pencil, X, Cpu } from "lucide-react";
import styles from "./agents.module.scss";
import { API_BASE } from "../../utils/constants";
import type { AgentRecord } from "../../types/agents";

const Agents = () => {
  const [agents, setAgents] = useState<AgentRecord[]>([]);
  const [models, setModels] = useState<string[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [model, setModel] = useState("");
  const [status, setStatus] = useState<"idle" | "submitting" | "success" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState("");
  const [loading, setLoading] = useState(true);

  const [editAgent, setEditAgent] = useState<AgentRecord | null>(null);
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editModel, setEditModel] = useState("");
  const [editStatus, setEditStatus] = useState<"idle" | "submitting" | "success" | "error">("idle");
  const [editErrorMsg, setEditErrorMsg] = useState("");

  const fetchAgents = async () => {
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/agents`);
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      const data: AgentRecord[] = await res.json();
      setAgents(data);
    } catch {
      // non-blocking
    } finally {
      setLoading(false);
    }
  };

  const fetchModels = async () => {
    try {
      const res = await fetch(`${API_BASE}/agent-runtime/assistant/models`);
      if (!res.ok) {
        return;
      }
      const data: string[] = await res.json();
      setModels(data);
      if (data.length > 0 && !model) {
        setModel(data[0]);
      }
    } catch {
      // non-blocking
    }
  };

  useEffect(() => {
    fetchAgents();
    fetchModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatus("submitting");
    setErrorMsg("");

    try {
      const res = await fetch(`${API_BASE}/agent-runtime/agents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          description: description.trim(),
          model: model.trim(),
        }),
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
      }

      setStatus("success");
      setName("");
      setDescription("");
      setModel(models[0] ?? "");
      await fetchAgents();
      setTimeout(() => setStatus("idle"), 2500);
    } catch (err: unknown) {
      setStatus("error");
      setErrorMsg(err instanceof Error ? err.message : "Unknown error");
    }
  };

  const openEdit = (agent: AgentRecord) => {
    setEditAgent(agent);
    setEditName(agent.name);
    setEditDescription(agent.description);
    setEditModel(agent.model || (models[0] ?? ""));
    setEditStatus("idle");
    setEditErrorMsg("");
  };

  const closeEdit = () => {
    setEditAgent(null);
    setEditStatus("idle");
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editAgent) {
      return;
    }
    setEditStatus("submitting");
    setEditErrorMsg("");

    try {
      const res = await fetch(`${API_BASE}/agent-runtime/agents/${editAgent.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: editAgent.id,
          name: editName.trim(),
          description: editDescription.trim(),
          model: editModel.trim(),
        }),
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
      }

      setEditStatus("success");
      await fetchAgents();
      setTimeout(() => closeEdit(), 1000);
    } catch (err: unknown) {
      setEditStatus("error");
      setEditErrorMsg(err instanceof Error ? err.message : "Unknown error");
    }
  };

  const handleDelete = async (agentId: string) => {
    if (!confirm("Are you sure you want to delete this agent?")) {
      return;
    }

    try {
      const res = await fetch(`${API_BASE}/agent-runtime/agents/${agentId}`, {
        method: "DELETE",
      });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
      }
      await fetchAgents();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : "Unknown error");
    }
  };

  return (
    <div className={styles.agentsContainer}>
      <div className={styles.agentsHeader}>
        <div>
          <h1 className={styles.agentsTitle}>Agent Registry</h1>
          <p className={styles.agentsSubtitle}>Register and manage autonomous agents in the pipeline</p>
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
                  value={model}
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
                  value={model}
                  onChange={(e) => setModel(e.target.value)}
                  placeholder="e.g. gemma3:1b"
                  autoComplete="off"
                  spellCheck={false}
                />
              )}
            </div>

            <button
              className={`${styles.submitBtn} ${status === "submitting" ? styles.submitting : ""}`}
              type="submit"
              disabled={status === "submitting" || !name.trim()}
            >
              {status === "submitting" ? (
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

            {status === "success" && (
              <div className={`${styles.statusMsg} ${styles.success}`}>
                <CheckCircle size={14} />
                Agent registered successfully
              </div>
            )}
            {status === "error" && (
              <div className={`${styles.statusMsg} ${styles.error}`}>
                <AlertCircle size={14} />
                {errorMsg}
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
            {loading && (
              <div className={styles.listPlaceholder}>
                <Loader size={20} className={styles.spinIcon} />
                <span>Loading registry...</span>
              </div>
            )}
            {!loading && agents.length === 0 && (
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
                      {new Date(agent.createdAt).toLocaleDateString("en-US", {
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
                className={`${styles.submitBtn} ${editStatus === "submitting" ? styles.submitting : ""}`}
                type="submit"
                disabled={editStatus === "submitting" || !editName.trim()}
              >
                {editStatus === "submitting" ? (
                  <>
                    <Loader size={14} className={styles.spinIcon} /> Saving...
                  </>
                ) : editStatus === "success" ? (
                  <>
                    <CheckCircle size={14} /> Saved
                  </>
                ) : (
                  "Save Changes"
                )}
              </button>

              {editStatus === "error" && (
                <div className={`${styles.statusMsg} ${styles.error}`}>
                  <AlertCircle size={14} />
                  {editErrorMsg}
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
