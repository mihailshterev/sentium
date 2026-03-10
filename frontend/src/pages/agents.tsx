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

export default function Agents() {
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
    <div className={styles["agents-container"]}>
      <div className={styles["agents-header"]}>
        <div className={styles["agents-title-row"]}>
          <span className={styles["agents-prompt"]}>&gt;</span>
          <h1 className={styles["agents-title"]}>AGENT_REGISTRY</h1>
        </div>
        <div className={styles["agents-subtitle"]}>
          Manage registered autonomous agents
        </div>
      </div>

      <div className={styles["agents-body"]}>
        <div className={styles["panel"] + " " + styles["create-panel"]}>
          <div className={styles["panel-header"]}>
            <span className={styles["panel-dot"]}></span>
            <span className={styles["panel-label"]}>CREATE_AGENT</span>
          </div>

          <form className={styles["create-form"]} onSubmit={handleSubmit}>
            <div className={styles["field-group"]}>
              <label className={styles["field-label"]}>&gt; AGENT_NAME</label>
              <input
                className={styles["field-input"]}
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

            <div className={styles["field-group"]}>
              <label className={styles["field-label"]}>&gt; DESCRIPTION</label>
              <textarea
                className={
                  styles["field-input"] + " " + styles["field-textarea"]
                }
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe agent capabilities..."
                maxLength={1000}
                rows={4}
                spellCheck={false}
              />
              <span className={styles["char-count"]}>
                {description.length}/1000
              </span>
            </div>

            <button
              className={`${styles["submit-btn"]} ${status === "submitting" ? styles["submitting"] : ""}`}
              type="submit"
              disabled={status === "submitting" || !name.trim()}
            >
              {status === "submitting" ? (
                <>
                  <span className={styles["btn-blink"]}>_</span> REGISTERING...
                </>
              ) : (
                <>
                  REGISTER AGENT<span className={styles["btn-cursor"]}>_</span>
                </>
              )}
            </button>

            {status === "success" && (
              <div className={styles["status-msg"] + " " + styles["success"]}>
                <span className={styles["status-icon"]}>✓</span> AGENT
                REGISTERED SUCCESSFULLY
              </div>
            )}
            {status === "error" && (
              <div className={styles["status-msg"] + " " + styles["error"]}>
                <span className={styles["status-icon"]}>✕</span> ERROR:{" "}
                {errorMsg}
              </div>
            )}
          </form>
        </div>

        <div className={styles["panel"] + " " + styles["list-panel"]}>
          <div className={styles["panel-header"]}>
            <span
              className={styles["panel-dot"] + " " + styles["active"]}
            ></span>
            <span className={styles["panel-label"]}>REGISTERED_AGENTS</span>
            <span className={styles["agent-count"]}>[{agents.length}]</span>
          </div>

          <div className={styles["agent-list"]}>
            {loading && (
              <div className={styles["list-placeholder"]}>
                <span>_</span> LOADING REGISTRY...
              </div>
            )}
            {!loading && agents.length === 0 && (
              <div className={styles["list-placeholder"]}>
                NO AGENTS REGISTERED
              </div>
            )}
            {agents.map((agent, i) => (
              <div className={styles["agent-entry"]} key={agent.id}>
                <div className={styles["agent-entry-header"]}>
                  <span className={styles["agent-index"]}>
                    [{String(i + 1).padStart(2, "0")}]
                  </span>
                  <span className={styles["agent-name"]}>{agent.name}</span>
                  <span className={styles["agent-status-badge"]}>ACTIVE</span>
                </div>
                <div className={styles["agent-description"]}>
                  {agent.description || "—"}
                </div>
                <div className={styles["agent-meta"]}>
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
}
