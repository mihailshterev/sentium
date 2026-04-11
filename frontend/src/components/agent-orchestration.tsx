import { useState, useEffect, useCallback, useRef } from "react";
import Markdown from "react-markdown";
import { Play, Bot, Zap, CheckCircle, Circle, Loader, Terminal, GitBranch } from "lucide-react";
import styles from "./agent-orchestration.module.scss";
import { API_BASE } from "../utils/constants";
import type { Phase, LogEntry, WorkflowRecord } from "../types/orchestration";
import type { AgentRecord } from "../types/agents";

const SCENARIOS = [
  {
    label: "Advanced Attack Scenario",
    payload: {
      activity:
        "ALERT: Home router admin login from internal IP, DNS servers changed to suspicious resolver, followed by multiple successful logins to banking and email accounts from foreign IPs. Compromised third-party HVAC vendor account in Azure AD via OAuth consent phishing. Attacker obtains Microsoft Graph token with Mail.Read and Files.Read.All and enumerates SharePoint for network diagrams and VPN configs. Impossible travel login observed (US → Poland → US) using residential proxy ASN.",
    },
  },
];

const PHASE_STEPS: { key: Phase; label: string; icon: React.ElementType }[] = [
  { key: "PLANNING", label: "Plan", icon: Circle },
  { key: "SQUAD", label: "Execute", icon: Zap },
  { key: "VALIDATING", label: "Validate", icon: CheckCircle },
];

const PHASE_ORDER: Phase[] = ["IDLE", "PLANNING", "SQUAD", "VALIDATING", "COMPLETE"];

const AgentOrchestration = () => {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [phase, setPhase] = useState<Phase>("IDLE");
  const [dbAgents, setDbAgents] = useState<AgentRecord[]>([]);
  const [workflows, setWorkflows] = useState<WorkflowRecord[]>([]);
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [scenarioInput, setScenarioInput] = useState("");

  const logsBufferRef = useRef<LogEntry[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const animationFrameRef = useRef<number | null>(null);

  useEffect(() => {
    fetch(`${API_BASE}/agent-runtime/agents`)
      .then((r) => (r.ok ? r.json() : []))
      .then((data: AgentRecord[]) => setDbAgents(data))
      .catch(() => {});

    fetch(`${API_BASE}/agent-runtime/workflows`)
      .then((r) => (r.ok ? r.json() : []))
      .then((data: WorkflowRecord[]) => setWorkflows(data))
      .catch(() => {});

    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, []);

  const openStream = useCallback((eventId: string) => {
    const eventSource = new EventSource(`${API_BASE}/agent-runtime/agents/stream/${eventId}`);

    const syncLogs = () => {
      setLogs([...logsBufferRef.current]);
      animationFrameRef.current = requestAnimationFrame(syncLogs);
    };

    animationFrameRef.current = requestAnimationFrame(syncLogs);

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
          const currentLogs = logsBufferRef.current;
          const lastIndex = currentLogs.length - 1;
          if (lastIndex >= 0 && currentLogs[lastIndex].Author === author) {
            currentLogs[lastIndex].Text += text;
          } else {
            currentLogs.push({ Author: author, Text: text });
          }
        }
      } catch (err) {
        console.error("Stream error:", err);
      }
    };

    eventSource.onerror = () => {
      eventSource.close();
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      setLogs([...logsBufferRef.current]);
      setPhase("COMPLETE");
    };
  }, []);

  const runAgent = useCallback(
    async (scenarioData?: Record<string, string>) => {
      logsBufferRef.current = [];
      setLogs([]);
      setPhase("PLANNING");

      const res = await fetch(`${API_BASE}/agent-runtime/agents/test-pipeline`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(scenarioData ?? { activity: "Manual Scan", user: "root" }),
      });
      const { eventId } = await res.json();
      openStream(eventId);
    },
    [openStream],
  );

  const runWorkflow = useCallback(async () => {
    if (!selectedWorkflow) {
      return;
    }

    const scenario = scenarioInput.trim() || `Execute workflow: ${selectedWorkflow.name}`;

    logsBufferRef.current = [];
    setLogs([]);
    setPhase("PLANNING");

    const res = await fetch(`${API_BASE}/agent-runtime/agents/run-workflow`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ workflowId: selectedWorkflow.id, scenario }),
    });
    const { eventId } = await res.json();
    openStream(eventId);
  }, [selectedWorkflow, scenarioInput, openStream]);

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
  const currentPhaseIndex = PHASE_ORDER.indexOf(phase);

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Terminal size={16} className={styles.headerIcon} />
          <div>
            <h2 className={styles.headerTitle}>Orchestration</h2>
            <span className={styles.headerSub}>Real-time multi-agent pipeline</span>
          </div>
        </div>

        <div className={styles.phaseBar}>
          {PHASE_STEPS.map((step, i) => {
            const stepIndex = PHASE_ORDER.indexOf(step.key);
            const isDone = currentPhaseIndex > stepIndex;
            const isActive = phase === step.key;
            const Icon = isDone ? CheckCircle : isActive ? Loader : step.icon;
            return (
              <div key={step.key} className={styles.phaseStep}>
                <div
                  className={`${styles.phaseNode} ${isActive ? styles.phaseNodeActive : ""} ${isDone ? styles.phaseNodeDone : ""}`}
                >
                  <Icon size={13} className={isActive ? styles.spinIcon : undefined} />
                </div>
                <span
                  className={`${styles.phaseLabel} ${isActive ? styles.phaseLabelActive : ""} ${isDone ? styles.phaseLabelDone : ""}`}
                >
                  {step.label}
                </span>
                {i < PHASE_STEPS.length - 1 && (
                  <div className={`${styles.phaseConnector} ${isDone ? styles.phaseConnectorDone : ""}`} />
                )}
              </div>
            );
          })}
        </div>
      </div>

      <div className={styles.body}>
        <aside className={styles.sidebar}>
          <div className={styles.sidebarSection}>
            <p className={styles.sidebarLabel}>Scenarios</p>
            <div className={styles.scenarioList}>
              {SCENARIOS.map((s) => (
                <button
                  key={s.label}
                  className={`${styles.scenarioBtn} ${!selectedWorkflow ? styles.scenarioBtnActive : ""}`}
                  onClick={() => {
                    setSelectedWorkflow(null);
                    runAgent(s.payload);
                  }}
                  disabled={isRunning}
                >
                  <Play size={12} className={styles.scenarioBtnIcon} />
                  <span>{s.label}</span>
                </button>
              ))}
            </div>
          </div>

          <div className={styles.sidebarSection}>
            <p className={styles.sidebarLabel}>
              Workflows
              <span className={styles.sidebarCount}>{workflows.length}</span>
            </p>
            <div className={styles.workflowList}>
              {workflows.length === 0 ? (
                <span className={styles.sidebarEmpty}>No workflows defined</span>
              ) : (
                workflows.map((wf) => (
                  <button
                    key={wf.id}
                    className={`${styles.workflowBtn} ${selectedWorkflow?.id === wf.id ? styles.workflowBtnActive : ""}`}
                    onClick={() => setSelectedWorkflow(wf)}
                    disabled={isRunning}
                  >
                    <GitBranch size={12} className={styles.workflowBtnIcon} />
                    <div className={styles.workflowBtnInfo}>
                      <span className={styles.workflowBtnName}>{wf.name}</span>
                      <span className={styles.workflowBtnMeta}>
                        {wf.agents.length} agent
                        {wf.agents.length !== 1 ? "s" : ""}
                      </span>
                    </div>
                  </button>
                ))
              )}
            </div>
          </div>

          {selectedWorkflow && (
            <div className={styles.sidebarSection}>
              <p className={styles.sidebarLabel}>Run: {selectedWorkflow.name}</p>
              <textarea
                className={styles.scenarioInput}
                value={scenarioInput}
                onChange={(e) => setScenarioInput(e.target.value)}
                placeholder="Describe the scenario for this workflow..."
                rows={3}
              />
              <button className={styles.runWorkflowBtn} onClick={runWorkflow} disabled={isRunning}>
                <Play size={13} />
                Execute Workflow
              </button>
            </div>
          )}

          <div className={styles.sidebarSection}>
            <p className={styles.sidebarLabel}>
              Agent Registry
              <span className={styles.sidebarCount}>{dbAgents.length}</span>
            </p>
            <div className={styles.agentRegistryList}>
              {dbAgents.length === 0 ? (
                <span className={styles.sidebarEmpty}>No custom agents</span>
              ) : (
                dbAgents.map((a) => (
                  <div className={styles.registryEntry} key={a.id}>
                    <Bot size={12} className={styles.registryIcon} />
                    <span className={styles.registryName}>{a.name}</span>
                  </div>
                ))
              )}
            </div>
          </div>

          <div className={styles.sidebarFooter}>
            <p className={styles.sidebarLabel}>Connection</p>
            <div className={styles.connectionInfo}>
              <div className={styles.connRow}>
                <span className={styles.connKey}>Host</span>
                <span className={styles.connVal}>127.0.0.1:7127</span>
              </div>
              <div className={styles.connRow}>
                <span className={styles.connKey}>Protocol</span>
                <span className={styles.connVal}>SSE/JSON</span>
              </div>
            </div>
          </div>
        </aside>

        <main className={styles.logPanel}>
          <div className={styles.logPanelHeader}>
            <span className={`${styles.logDot} ${isRunning ? styles.logDotActive : ""}`}></span>
            <span className={styles.logPanelTitle}>Output</span>
            {phase === "COMPLETE" && (
              <span className={styles.completeBadge}>
                <CheckCircle size={11} /> Complete
              </span>
            )}
          </div>

          <div className={styles.logWindow} ref={scrollRef}>
            {logs.length === 0 && phase === "IDLE" && (
              <div className={styles.logIdle}>
                <Terminal size={32} className={styles.logIdleIcon} />
                <p>Select a scenario or workflow to start the pipeline</p>
                <span>Output will stream here in real time</span>
              </div>
            )}

            {logs.map((log, i) => (
              <div key={i} className={styles.logEntry}>
                <div className={styles.authorRow}>
                  <span className={`${styles.roleBadge} ${getRoleClass(log.Author)}`}>{log.Author}</span>
                  <div className={styles.authorLine}></div>
                </div>
                <div className={styles.textContent}>
                  <Markdown>{log.Text}</Markdown>
                </div>
              </div>
            ))}

            {isRunning && (
              <div className={styles.streamingIndicator}>
                <span className={styles.cursor}></span>
                <span className={styles.streamingLabel}>Streaming...</span>
              </div>
            )}
          </div>
        </main>
      </div>
    </div>
  );
};

export default AgentOrchestration;
