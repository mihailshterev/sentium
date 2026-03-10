import { useState, useRef, useEffect, useCallback } from "react";
import Markdown from "react-markdown";
import styles from "./agent-orchestration.module.scss";

const API_BASE = "https://localhost:7127";

interface LogEntry {
  Author: string;
  Text: string;
}

interface AgentRecord {
  id: string;
  name: string;
  description: string;
}

type Phase = "IDLE" | "PLANNING" | "SQUAD" | "VALIDATING" | "COMPLETE";

const SCENARIOS = [
  {
    label: "Advanced Attack Scenario",
    payload: {
      activity:
        "ALERT: Home router admin login from internal IP, DNS servers changed to suspicious resolver, followed by multiple successful logins to banking and email accounts from foreign IPs. Compromised third-party HVAC vendor account in Azure AD via OAuth consent phishing. Attacker obtains Microsoft Graph token with Mail.Read and Files.Read.All and enumerates SharePoint for network diagrams and VPN configs. Impossible travel login observed (US → Poland → US) using residential proxy ASN.",
    },
  },
];

export default function SentiumTerminal() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [phase, setPhase] = useState<Phase>("IDLE");
  const [dbAgents, setDbAgents] = useState<AgentRecord[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetch(`${API_BASE}/agents`)
      .then((r) => (r.ok ? r.json() : []))
      .then((data: AgentRecord[]) => setDbAgents(data))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [logs]);

  const runAgent = useCallback(
    async (scenarioData?: Record<string, string>) => {
      setLogs([]);
      setPhase("PLANNING");

      const eventSource = new EventSource(
        `${API_BASE}/agents/stream/events.network.scan`,
      );

      eventSource.onmessage = (e) => {
        if (!e.data || e.data === "null") return;
        try {
          const data = JSON.parse(e.data);
          const author: string = data.Author || data.author || "Agent";
          const text: string = data.Text || data.text || "";

          const lowerAuthor = author.toLowerCase();
          if (lowerAuthor.includes("planner")) setPhase("PLANNING");
          else if (lowerAuthor.includes("validator")) setPhase("VALIDATING");
          else setPhase("SQUAD");

          if (text) {
            setLogs((prev) => {
              if (prev.length > 0 && prev[prev.length - 1].Author === author) {
                const updated = [...prev];
                updated[updated.length - 1] = {
                  ...updated[updated.length - 1],
                  Text: updated[updated.length - 1].Text + text,
                };
                return updated;
              }
              return [...prev, { Author: author, Text: text }];
            });
          }
        } catch (err) {
          console.error(err);
        }
      };

      await new Promise((resolve) => setTimeout(resolve, 600));

      await fetch(`${API_BASE}/agents/test-pipeline`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(
          scenarioData ?? { activity: "Manual Scan", user: "root" },
        ),
      });

      eventSource.onerror = () => {
        eventSource.close();
        setPhase("COMPLETE");
      };
    },
    [],
  );

  const getRoleClass = (author: string) => {
    const a = author.toLowerCase();
    if (a.includes("security")) return "role-security";
    if (a.includes("summarizer")) return "role-summarizer";
    if (a.includes("forensics")) return "role-forensics";
    if (a.includes("intel")) return "role-intel";
    if (a.includes("planner")) return "role-planner";
    if (a.includes("validator")) return "role-validator";
    return "role-squad";
  };

  const isRunning = phase !== "IDLE" && phase !== "COMPLETE";

  return (
    <div className={styles["terminal-container"]}>
      <div className={styles["terminal-card"]}>
        {/* ── Header ── */}
        <header className={styles["terminal-header"]}>
          <div className={styles["brand-section"]}>
            <div className={styles["status-dot"]}></div>
            <div>
              <h2 className={styles["terminal-title"]}>
                SENTIUM // ORCHESTRATION_LOGS
              </h2>
              <div className={styles["terminal-subtitle"]}>
                SYSTEM_STATUS: ONLINE
              </div>
            </div>
          </div>

          <div className={styles["phase-tracker"]}>
            {(["PLANNING", "SQUAD", "VALIDATING"] as const).map((p, i) => {
              const labels = ["1. PLAN", "2. EXECUTE", "3. VALIDATE"];
              const order = ["PLANNING", "SQUAD", "VALIDATING", "COMPLETE"];
              const isDone = order.indexOf(phase) > order.indexOf(p);
              const isActive = phase === p;
              return (
                <div
                  key={p}
                  className={`${styles["phase-badge"]}${isActive ? ` ${styles["active"]}` : ""}${isDone ? ` ${styles["done"]}` : ""}`}
                >
                  {labels[i]}
                </div>
              );
            })}
          </div>
        </header>

        <div className={styles["terminal-body"]}>
          {/* ── Sidebar ── */}
          <aside className={styles["terminal-sidebar"]}>
            <div className={styles["sidebar-group"]}>
              <p className={styles["sidebar-label"]}>ATTACK_VECTORS</p>
              <div className={styles["scenario-list"]}>
                {SCENARIOS.map((s) => (
                  <button
                    key={s.label}
                    className={styles["scenario-btn"]}
                    onClick={() => runAgent(s.payload)}
                    disabled={isRunning}
                  >
                    &gt; {s.label}
                  </button>
                ))}
              </div>
            </div>

            <div className={styles["sidebar-group"]}>
              <p className={styles["sidebar-label"]}>
                AGENT_REGISTRY
                <span className={styles["sidebar-count"]}>
                  [{dbAgents.length}]
                </span>
              </p>
              <div className={styles["agent-registry-list"]}>
                {dbAgents.length === 0 ? (
                  <span className={styles["sidebar-empty"]}>
                    No custom agents
                  </span>
                ) : (
                  dbAgents.map((a) => (
                    <div className={styles["registry-entry"]} key={a.id}>
                      <span className={styles["registry-dot"]}></span>
                      <span className={styles["registry-name"]}>{a.name}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            <div
              className={`${styles["sidebar-group"]} ${styles["sidebar-group--bottom"]}`}
            >
              <p className={styles["sidebar-label"]}>ACTIVE_SESSION</p>
              <div className={styles["session-meta"]}>
                <span>IP: 192.168.1.104</span>
                <span>PORT: 7127</span>
                <span>PROTO: SSE/JSON</span>
              </div>
            </div>
          </aside>

          {/* ── Log Window ── */}
          <main className={styles["log-window"]} ref={scrollRef}>
            {logs.length === 0 && phase === "IDLE" && (
              <div className={styles["log-idle"]}>
                <span className={styles["log-idle-prompt"]}>&gt; </span>
                Awaiting scenario trigger...
              </div>
            )}

            {logs.map((log, i) => (
              <div key={i} className={styles["log-entry"]}>
                <div className={styles["author-meta"]}>
                  <span
                    className={`${styles["role-badge"]} ${getRoleClass(log.Author)}`}
                  >
                    {log.Author.toUpperCase()}
                  </span>
                  <div className={styles["author-divider"]}></div>
                </div>
                <div className={styles["text-content"]}>
                  <Markdown>{log.Text}</Markdown>
                </div>
              </div>
            ))}

            {isRunning && (
              <div className="log-entry">
                <span className="cursor"></span>
              </div>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
