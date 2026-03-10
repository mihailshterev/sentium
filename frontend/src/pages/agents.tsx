import { useState, useEffect } from "react";
import styles from "./agents.module.scss";

const API_BASE = "https://localhost:7127";

interface AgentRecord {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
}

const Agents = () => {
  const [agents, setAgents] = useState<AgentRecord[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState<
    "idle" | "submitting" | "success" | "error"
  >("idle");
  const [errorMsg, setErrorMsg] = useState("");
  const [loading, setLoading] = useState(true);

  const fetchAgents = async () => {
    try {
      const res = await fetch(`${API_BASE}/agents`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: AgentRecord[] = await res.json();
      setAgents(data);
    } catch {
      // list fetch failure is non-blocking
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAgents();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatus("submitting");
    setErrorMsg("");

    try {
      const res = await fetch(`${API_BASE}/agents`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          description: description.trim(),
        }),
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
      }

      setStatus("success");
      setName("");
      setDescription("");
      await fetchAgents();
      setTimeout(() => setStatus("idle"), 2500);
    } catch (err: unknown) {
      setStatus("error");
      setErrorMsg(err instanceof Error ? err.message : "Unknown error");
    }
  };

  return (
    <div className={styles.agentsContainer}>
      <div className={styles.agentsHeader}>
        <div className={styles.agentsTitleRow}>
          <span className={styles.agentsPrompt}>&gt;</span>
          <h1 className={styles.agentsTitle}>AGENT_REGISTRY</h1>
        </div>
        <div className={styles.agentsSubtitle}>
          Manage registered autonomous agents
        </div>
      </div>

      <div className={styles.agentsBody}>
        <div className={styles.panel + " " + styles.createPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot}></span>
            <span className={styles.panelLabel}>CREATE_AGENT</span>
          </div>

          <form className={styles.createForm} onSubmit={handleSubmit}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel}>&gt; AGENT_NAME</label>
              <input
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
              <label className={styles.fieldLabel}>&gt; DESCRIPTION</label>
              <textarea
                className={styles.fieldInput + " " + styles.fieldTextarea}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe agent capabilities..."
                maxLength={1000}
                rows={4}
                spellCheck={false}
              />
              <span className={styles.charCount}>
                {description.length}/1000
              </span>
            </div>

            <button
              className={`${styles.submitBtn} ${status === "submitting" ? styles.submitting : ""}`}
              type="submit"
              disabled={status === "submitting" || !name.trim()}
            >
              {status === "submitting" ? (
                <>
                  <span className={styles.btnBlink}>_</span> REGISTERING...
                </>
              ) : (
                <>
                  REGISTER AGENT<span className={styles.btnCursor}>_</span>
                </>
              )}
            </button>

            {status === "success" && (
              <div className={styles.statusMsg + " " + styles.success}>
                <span className={styles.statusIcon}>✓</span> AGENT REGISTERED
                SUCCESSFULLY
              </div>
            )}
            {status === "error" && (
              <div className={styles.statusMsg + " " + styles.error}>
                <span className={styles.statusIcon}>✕</span> ERROR: {errorMsg}
              </div>
            )}
          </form>
        </div>

        <div className={styles.panel + " " + styles.listPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot + " " + styles.active}></span>
            <span className={styles.panelLabel}>REGISTERED_AGENTS</span>
            <span className={styles.agentCount}>[{agents.length}]</span>
          </div>

          <div className={styles.agentList}>
            {loading && (
              <div className={styles.listPlaceholder}>
                <span>_</span> LOADING REGISTRY...
              </div>
            )}
            {!loading && agents.length === 0 && (
              <div className={styles.listPlaceholder}>NO AGENTS REGISTERED</div>
            )}
            {agents.map((agent, i) => (
              <div className={styles.agentEntry} key={agent.id}>
                <div className={styles.agentEntryHeader}>
                  <span className={styles.agentIndex}>
                    [{String(i + 1).padStart(2, "0")}]
                  </span>
                  <span className={styles.agentName}>{agent.name}</span>
                  <span className={styles.agentStatusBadge}>ACTIVE</span>
                </div>
                <div className={styles.agentDescription}>
                  {agent.description || "—"}
                </div>
                <div className={styles.agentMeta}>
                  <span>ID: {agent.id.slice(0, 8)}...</span>
                  <span>
                    REGISTERED:{" "}
                    {new Date(agent.createdAt).toISOString().slice(0, 10)}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Agents;
