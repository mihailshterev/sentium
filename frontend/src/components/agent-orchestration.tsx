import { useState, useEffect, useCallback } from "react";
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

const AgentOrchestration = () => {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [phase, setPhase] = useState<Phase>("IDLE");
  const [dbAgents, setDbAgents] = useState<AgentRecord[]>([]);

  useEffect(() => {
    fetch(`${API_BASE}/agents`)
      .then((r) => (r.ok ? r.json() : []))
      .then((data: AgentRecord[]) => setDbAgents(data))
      .catch(() => {});
  }, []);

  const runAgent = useCallback(
    async (scenarioData?: Record<string, string>) => {
      setLogs([]);
      setPhase("PLANNING");

      const eventSource = new EventSource(
        `${API_BASE}/agents/stream/events.network.scan`,
      );

      eventSource.onmessage = (e) => {
        if (!e.data || e.data === "null") {
          return;
        }

        try {
          const data = JSON.parse(e.data);
          const author: string = data.Author || data.author || "Agent";
          const text: string = data.Text || data.text || "";

          const lowerAuthor = author.toLowerCase();
          if (lowerAuthor.includes("planner")) {
            setPhase("PLANNING");
          } else if (lowerAuthor.includes("validator")) {
            setPhase("VALIDATING");
          } else {
            setPhase("SQUAD");
          }

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
    if (a.includes("security")) return "roleSecurity";
    if (a.includes("summarizer")) return "roleSummarizer";
    if (a.includes("forensics")) return "roleForensics";
    if (a.includes("intel")) return "roleIntel";
    if (a.includes("planner")) return "rolePlanner";
    if (a.includes("validator")) return "roleValidator";
    return "roleSquad";
  };

  const isRunning = phase !== "IDLE" && phase !== "COMPLETE";

  return (
    <div className={styles.terminalContainer}>
      <div className={styles.terminalHeader}>
        <div className={styles.titleRow}>
          <span className={styles.pagePrompt}>&gt;</span>
          <h2 className={styles.terminalTitle}>ORCHESTRATION_LOGS</h2>
        </div>
        <div className={styles.terminalSubtitle}>SYSTEM_STATUS: ONLINE</div>
      </div>

      <div className={styles.terminalBody}>
        <aside className={styles.terminalSidebar}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot}></span>
            <span className={styles.panelLabel}>CONTROLS</span>
          </div>
          <div className={styles.sidebarInner}>
            <div className={styles.sidebarGroup}>
              <p className={styles.sidebarLabel}>ATTACK_VECTORS</p>
              <div className={styles.scenarioList}>
                {SCENARIOS.map((s) => (
                  <button
                    key={s.label}
                    className={styles.scenarioBtn}
                    onClick={() => runAgent(s.payload)}
                    disabled={isRunning}
                  >
                    &gt; {s.label}
                  </button>
                ))}
              </div>
            </div>

            <div className={styles.sidebarGroup}>
              <p className={styles.sidebarLabel}>
                AGENT_REGISTRY
                <span className={styles.sidebarCount}>[{dbAgents.length}]</span>
              </p>
              <div className={styles.agentRegistryList}>
                {dbAgents.length === 0 ? (
                  <span className={styles.sidebarEmpty}>No custom agents</span>
                ) : (
                  dbAgents.map((a) => (
                    <div className={styles.registryEntry} key={a.id}>
                      <span className={styles.registryDot}></span>
                      <span className={styles.registryName}>{a.name}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            <div
              className={`${styles.sidebarGroup} ${styles.sidebarGroupBottom}`}
            >
              <p className={styles.sidebarLabel}>ACTIVE_SESSION</p>
              <div className={styles.sessionMeta}>
                <span>IP: 192.168.1.104</span>
                <span>PORT: 7127</span>
                <span>PROTO: SSE/JSON</span>
              </div>
            </div>
          </div>
        </aside>

        <main className={styles.logPanel}>
          <div className={styles.panelHeader}>
            <span className={`${styles.panelDot} ${styles.dotActive}`}></span>
            <span className={styles.panelLabel}>LOG_OUTPUT</span>
            <div className={styles.phaseTracker}>
              {(["PLANNING", "SQUAD", "VALIDATING"] as const).map((p, i) => {
                const labels = ["1. PLAN", "2. EXECUTE", "3. VALIDATE"];
                const order = ["PLANNING", "SQUAD", "VALIDATING", "COMPLETE"];
                const isDone = order.indexOf(phase) > order.indexOf(p);
                const isActive = phase === p;
                return (
                  <div
                    key={p}
                    className={`${styles.phaseBadge}${isActive ? ` ${styles.active}` : ""}${isDone ? ` ${styles.done}` : ""}`}
                  >
                    {labels[i]}
                  </div>
                );
              })}
            </div>
          </div>
          <div className={styles.logWindow}>
            {logs.length === 0 && phase === "IDLE" && (
              <div className={styles.logIdle}>
                <span className={styles.logIdlePrompt}>&gt; </span>
                Awaiting scenario trigger...
              </div>
            )}

            {logs.map((log, i) => (
              <div key={i} className={styles.logEntry}>
                <div className={styles.authorMeta}>
                  <span
                    className={`${styles.roleBadge} ${getRoleClass(log.Author)}`}
                  >
                    {log.Author.toUpperCase()}
                  </span>
                  <div className={styles.authorDivider}></div>
                </div>
                <div className={styles.textContent}>
                  <Markdown>{log.Text}</Markdown>
                </div>
              </div>
            ))}

            {isRunning && (
              <div className={styles.logEntry}>
                <span className={styles.cursor}></span>
              </div>
            )}
          </div>
        </main>
      </div>
    </div>
  );
};

export default AgentOrchestration;
