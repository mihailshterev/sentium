import { useState, useEffect, useCallback, useRef } from "react";
import Markdown from "react-markdown";
import {
  Play,
  Zap,
  CheckCircle,
  Circle,
  Loader,
  Terminal,
  GitBranch,
  Brain,
  Wrench,
  ChevronDown,
  History,
  FolderOpen,
  Clock,
  Orbit,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import styles from "./agent-orchestration.module.scss";
import { runWorkflowPipeline, fetchWorkflowRuns, fetchWorkspaces } from "../../services/agentRuntime.service";
import useWorkflows from "../../hooks/useWorkflows";
import type { Phase, LogEntry } from "../../types/orchestration";
import type { WorkflowRecord } from "../../types/workflows";
import type { WorkflowRun } from "../../types/workflowRuns";
import { BASE_URL } from "../../api/client";

const PHASE_STEPS: { key: Phase; label: string; icon: React.ElementType }[] = [
  { key: "PLANNING", label: "Plan", icon: Circle },
  { key: "SQUAD", label: "Execute", icon: Zap },
  { key: "VALIDATING", label: "Validate", icon: CheckCircle },
];

const PHASE_ORDER: Phase[] = ["IDLE", "PLANNING", "SQUAD", "VALIDATING", "COMPLETE"];

interface LogEntryViewProps {
  log: LogEntry;
  entryId: string;
  expanded: boolean;
  onToggle: (id: string) => void;
  getRoleClass: (author: string) => string;
}

const LogEntryView = ({ log, entryId, expanded, onToggle, getRoleClass }: LogEntryViewProps) => {
  if (log.type === "thought") {
    return (
      <div className={styles.logEntry}>
        <div className={styles.authorRow}>
          <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
          <div className={styles.authorLine} />
        </div>
        <div className={styles.thoughtBlock}>
          <button className={styles.thoughtHeader} onClick={() => onToggle(entryId)}>
            <Brain size={11} />
            <span>Thinking</span>
            <ChevronDown
              size={11}
              className={`${styles.thoughtChevron} ${expanded ? styles.thoughtChevronOpen : ""}`}
            />
          </button>
          {expanded && (
            <div className={styles.thoughtContent}>
              <Markdown>{log.text}</Markdown>
            </div>
          )}
        </div>
      </div>
    );
  }

  if (log.type === "tool") {
    return (
      <div className={styles.toolCallEntry}>
        <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
        <div className={styles.toolCallRow}>
          <Wrench size={10} />
          <span>{log.text}</span>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.logEntry}>
      <div className={styles.authorRow}>
        <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
        <div className={styles.authorLine} />
      </div>
      <div className={styles.textContent}>
        <Markdown>{log.text}</Markdown>
      </div>
    </div>
  );
};

function coalesceLog(entries: LogEntry[]): LogEntry[] {
  const result: LogEntry[] = [];
  for (const entry of entries) {
    const last = result[result.length - 1];
    if (
      last &&
      (entry.type === "message" || entry.type === "thought") &&
      last.type === entry.type &&
      last.author === entry.author
    ) {
      last.text += entry.text;
    } else {
      result.push({ ...entry });
    }
  }
  return result;
}

const AgentOrchestration = () => {
  const { workflows } = useWorkflows();

  const [sidebarView, setSidebarView] = useState<"execute" | "history">("execute");
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [phase, setPhase] = useState<Phase>("IDLE");
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [scenarioInput, setScenarioInput] = useState("");
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string>("");
  const [selectedRun, setSelectedRun] = useState<WorkflowRun | null>(null);
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());

  const logsBufferRef = useRef<LogEntry[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const animationFrameRef = useRef<number | null>(null);

  const { data: workspaces = [] } = useQuery({
    queryKey: ["workspaces"],
    queryFn: fetchWorkspaces,
  });

  const { data: workflowRuns = [], refetch: refetchRuns } = useQuery({
    queryKey: ["workflowRuns"],
    queryFn: () => fetchWorkflowRuns(30),
    enabled: sidebarView === "history",
    refetchInterval: sidebarView === "history" ? 15_000 : false,
  });

  useEffect(() => {
    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, []);

  const toggleThought = (id: string) =>
    setExpandedThoughts((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });

  const openStream = useCallback(
    (eventId: string) => {
      const eventSource = new EventSource(`${BASE_URL}/agent-runtime/agents/stream/${eventId}`, {
        withCredentials: true,
      });

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
          const data = JSON.parse(e.data) as {
            Author?: string;
            author?: string;
            Text?: string;
            text?: string;
            Type?: string;
            type?: string;
          };
          const author = data.Author ?? data.author ?? "Agent";
          const text = data.Text ?? data.text ?? "";
          const type = (data.Type ?? data.type ?? "message") as LogEntry["type"];

          if (type === "message") {
            const a = author.toLowerCase();
            if (a.includes("planner")) {
              setPhase("PLANNING");
            } else if (a.includes("validator")) {
              setPhase("VALIDATING");
            } else {
              setPhase("SQUAD");
            }
          }

          if (text) {
            const current = logsBufferRef.current;
            const last = current.length - 1;
            if (
              (type === "message" || type === "thought") &&
              last >= 0 &&
              current[last].author === author &&
              current[last].type === type
            ) {
              current[last].text += text;
            } else {
              current.push({ author, text, type });
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
        void refetchRuns();
      };
    },
    [refetchRuns],
  );

  const runWorkflow = useCallback(async () => {
    if (!selectedWorkflow) {
      return;
    }
    const scenario = scenarioInput.trim() || `Execute workflow: ${selectedWorkflow.name}`;
    logsBufferRef.current = [];
    setLogs([]);
    setSelectedRun(null);
    setPhase("PLANNING");
    setSidebarView("execute");
    const { eventId } = await runWorkflowPipeline({
      workflowId: selectedWorkflow.id,
      scenario,
      ...(selectedWorkspaceId && { workspaceId: selectedWorkspaceId }),
    });
    openStream(eventId);
  }, [selectedWorkflow, scenarioInput, selectedWorkspaceId, openStream]);

  const loadRun = (run: WorkflowRun) => {
    setSelectedRun(run);
    setLogs([]);
    logsBufferRef.current = [];
    setPhase("COMPLETE");
    setExpandedThoughts(new Set());
  };

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
  const displayLogs = selectedRun ? coalesceLog(selectedRun.logs) : logs;
  const displayPhase: Phase = selectedRun ? "COMPLETE" : phase;
  const displayPhaseIndex = PHASE_ORDER.indexOf(displayPhase);

  const formatRunLabel = (run: WorkflowRun) => {
    const d = new Date(run.startedAt);
    return `${d.toLocaleDateString("en-GB", { month: "short", day: "numeric" })} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  };

  const formatRunTrigger = (type: string) => type.split(".").filter(Boolean).pop() ?? type;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Orbit size={20} className={styles.headerIcon} />
          <div>
            <h2 className={styles.headerTitle}>Orchestration</h2>
            <span className={styles.headerSub}>Real-time multi-agent pipeline</span>
          </div>
        </div>

        <div className={styles.phaseBar}>
          {PHASE_STEPS.map((step, i) => {
            const stepIndex = PHASE_ORDER.indexOf(step.key);
            const isDone = displayPhaseIndex > stepIndex;
            const isActive = displayPhase === step.key;
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
          <div className={styles.sidebarTabs}>
            <button
              className={`${styles.sidebarTab} ${sidebarView === "execute" ? styles.sidebarTabActive : ""}`}
              onClick={() => setSidebarView("execute")}
            >
              <Play size={11} />
              Execute
            </button>
            <button
              className={`${styles.sidebarTab} ${sidebarView === "history" ? styles.sidebarTabActive : ""}`}
              onClick={() => setSidebarView("history")}
            >
              <History size={11} />
              History
            </button>
          </div>

          {sidebarView === "execute" && (
            <>
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
                            {wf.agents.length} agent{wf.agents.length !== 1 ? "s" : ""}
                          </span>
                        </div>
                      </button>
                    ))
                  )}
                </div>
              </div>

              {selectedWorkflow && (
                <>
                  <div className={styles.sidebarSection}>
                    <p className={styles.sidebarLabel}>
                      Workspace
                      <span className={styles.sidebarOptional}>optional</span>
                    </p>
                    <select
                      className={styles.workspaceSelect}
                      value={selectedWorkspaceId}
                      onChange={(e) => setSelectedWorkspaceId(e.target.value)}
                      disabled={isRunning}
                    >
                      <option value="">No workspace</option>
                      {workspaces.map((ws) => (
                        <option key={ws.id} value={ws.id}>
                          {ws.name}
                          {ws.fileCount > 0 ? ` (${ws.fileCount} files)` : ""}
                        </option>
                      ))}
                    </select>
                    {selectedWorkspaceId && (
                      <p className={styles.workspaceHint}>
                        <FolderOpen size={10} />
                        Agents can read and write files in this workspace
                      </p>
                    )}
                  </div>

                  <div className={styles.sidebarSection}>
                    <p className={styles.sidebarLabel}>Run: {selectedWorkflow.name}</p>
                    <textarea
                      className={styles.scenarioInput}
                      value={scenarioInput}
                      onChange={(e) => setScenarioInput(e.target.value)}
                      placeholder="Describe the scenario for this workflow..."
                      rows={3}
                      disabled={isRunning}
                    />
                    <button className={styles.runWorkflowBtn} onClick={runWorkflow} disabled={isRunning}>
                      {isRunning ? <Loader size={13} className={styles.spinIcon} /> : <Play size={13} />}
                      {isRunning ? "Running..." : "Execute Workflow"}
                    </button>
                  </div>
                </>
              )}
            </>
          )}

          {sidebarView === "history" && (
            <div
              className={styles.sidebarSection}
              style={{ flex: 1, overflow: "hidden", display: "flex", flexDirection: "column" }}
            >
              <p className={styles.sidebarLabel}>
                Recent Runs
                <span className={styles.sidebarCount}>{workflowRuns.length}</span>
              </p>
              <div className={styles.runList}>
                {workflowRuns.length === 0 && <div className={styles.sidebarEmpty}>No runs recorded yet</div>}
                {workflowRuns.map((run) => (
                  <button
                    key={run.id}
                    className={`${styles.runItem} ${selectedRun?.id === run.id ? styles.runItemActive : ""}`}
                    onClick={() => loadRun(run)}
                  >
                    <div className={styles.runItemHeader}>
                      <span className={styles.runTrigger}>{formatRunTrigger(run.triggerType)}</span>
                      <span className={styles.runLogs}>{run.logs.length} entries</span>
                    </div>
                    <div className={styles.runMeta}>
                      <Clock size={9} />
                      <span>{formatRunLabel(run)}</span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          <div className={styles.sidebarFooter}>
            <p className={styles.sidebarLabel}>Connection</p>
            <div className={styles.connectionInfo}>
              <div className={styles.connRow}>
                <span className={styles.connKey}>Protocol</span>
                <span className={styles.connVal}>SSE / NATS</span>
              </div>
              <div className={styles.connRow}>
                <span className={styles.connKey}>Status</span>
                <span className={`${styles.connVal} ${isRunning ? styles.connValActive : ""}`}>
                  {isRunning ? "Streaming" : "Idle"}
                </span>
              </div>
            </div>
          </div>
        </aside>

        <main className={styles.logPanel}>
          <div className={styles.logPanelHeader}>
            <span className={`${styles.logDot} ${isRunning ? styles.logDotActive : ""}`} />
            <span className={styles.logPanelTitle}>
              {selectedRun ? `Replay — ${formatRunTrigger(selectedRun.triggerType)}` : "Output"}
            </span>
            {displayPhase === "COMPLETE" && !isRunning && (
              <span className={styles.completeBadge}>
                <CheckCircle size={11} /> Complete
              </span>
            )}
            {selectedRun && (
              <span className={styles.historyBadge}>
                <History size={10} /> {formatRunLabel(selectedRun)}
              </span>
            )}
          </div>

          <div className={styles.logWindow} ref={scrollRef}>
            {displayLogs.length === 0 && !isRunning && !selectedRun && (
              <div className={styles.logIdle}>
                <Terminal size={32} className={styles.logIdleIcon} />
                <p>Select a workflow and execute to start the pipeline</p>
                <span>Output streams here in real time &bull; History tab shows past runs</span>
              </div>
            )}

            {displayLogs.length === 0 && selectedRun && (
              <div className={styles.logIdle}>
                <History size={28} className={styles.logIdleIcon} />
                <p>No log entries recorded for this run</p>
              </div>
            )}

            {displayLogs.map((log, i) => {
              const entryId = `${selectedRun?.id ?? "live"}-${i}`;
              return (
                <LogEntryView
                  key={entryId}
                  log={log}
                  entryId={entryId}
                  expanded={expandedThoughts.has(entryId)}
                  onToggle={toggleThought}
                  getRoleClass={getRoleClass}
                />
              );
            })}

            {isRunning && (
              <div className={styles.streamingIndicator}>
                <span className={styles.cursor} />
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
