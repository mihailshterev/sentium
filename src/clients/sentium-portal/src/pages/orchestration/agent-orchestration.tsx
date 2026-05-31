import { useState, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router";
import { Zap, CheckCircle, Circle, Loader, Terminal, History, Orbit } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import styles from "./agent-orchestration.module.scss";
import {
  runWorkflowPipeline,
  runDynamicWorkflow,
  fetchWorkflowRuns,
  fetchWorkspaces,
} from "../../services/agentRuntime.service";
import useWorkflows from "../../hooks/useWorkflows";
import { useWorkflowRun } from "../../hooks/useWorkflowRuns";
import type { Phase, LogEntry } from "../../types/orchestration";
import type { WorkflowRecord } from "../../types/workflows";
import type { WorkflowRun } from "../../types/workflows";
import { BASE_URL } from "../../api/client";
import PageHeader from "../../components/ui/page-header";
import LogEntryView from "./components/log-entry-view";
import ExecuteSidebar, { type ExecuteMode } from "./components/execute-sidebar";

const PHASE_STEPS: { key: Phase; label: string; icon: React.ElementType }[] = [
  { key: "PLANNING", label: "Plan", icon: Circle },
  { key: "SQUAD", label: "Execute", icon: Zap },
  { key: "VALIDATING", label: "Validate", icon: CheckCircle },
];

const PHASE_ORDER: Phase[] = ["IDLE", "PLANNING", "SQUAD", "VALIDATING", "COMPLETE"];

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
  const { runId } = useParams<{ runId?: string }>();
  const navigate = useNavigate();

  const [sidebarView, setSidebarView] = useState<"execute" | "history">(runId ? "history" : "execute");
  const [executeMode, setExecuteMode] = useState<ExecuteMode>("predefined");
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [phase, setPhase] = useState<Phase>("IDLE");
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [scenarioInput, setScenarioInput] = useState("");
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string>("");
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());

  const scrollRef = useRef<HTMLDivElement>(null);

  const { run: selectedRun, isLoading: runLoading, error: runError } = useWorkflowRun(runId);

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
      const eventSource = new EventSource(`${BASE_URL}/agent-runtime/orchestration/stream/${eventId}`, {
        withCredentials: true,
      });

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
            setLogs((prev) => {
              const last = prev.length - 1;
              if (
                (type === "message" || type === "thought") &&
                last >= 0 &&
                prev[last].author === author &&
                prev[last].type === type
              ) {
                const updated = [...prev];
                updated[last] = { ...prev[last], text: prev[last].text + text };
                return updated;
              }
              return [...prev, { author, text, type }];
            });
          }
        } catch (err) {
          console.error("Stream error:", err);
        }
      };

      eventSource.onerror = () => {
        eventSource.close();
        setPhase("COMPLETE");
        void refetchRuns();
      };
    },
    [refetchRuns],
  );

  const startLiveRun = useCallback(() => {
    setLogs([]);
    setPhase("PLANNING");
    setSidebarView("execute");
    if (runId) {
      navigate("/orchestration");
    }
  }, [runId, navigate]);

  const runWorkflow = useCallback(async () => {
    if (!selectedWorkflow) {
      return;
    }
    const scenario = scenarioInput.trim() || `Execute workflow: ${selectedWorkflow.name}`;
    startLiveRun();
    const { eventId } = await runWorkflowPipeline({
      workflowId: selectedWorkflow.id,
      scenario,
      ...(selectedWorkspaceId && { workspaceId: selectedWorkspaceId }),
    });
    openStream(eventId);
  }, [selectedWorkflow, scenarioInput, selectedWorkspaceId, openStream, startLiveRun]);

  const runDynamic = useCallback(async () => {
    const scenario = scenarioInput.trim();
    if (!scenario) {
      return;
    }
    startLiveRun();
    const { eventId } = await runDynamicWorkflow({
      activity: scenario,
      ...(selectedWorkspaceId && { workspaceId: selectedWorkspaceId }),
    });
    openStream(eventId);
  }, [scenarioInput, selectedWorkspaceId, openStream, startLiveRun]);

  const loadRun = (run: WorkflowRun) => {
    setExpandedThoughts(new Set());
    navigate(`/orchestration/runs/${run.id}`);
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

  const viewingRun = !!runId;
  const isRunning = !viewingRun && phase !== "IDLE" && phase !== "COMPLETE";
  const displayLogs = viewingRun && selectedRun ? coalesceLog(selectedRun.logs) : logs;
  const displayPhase: Phase = viewingRun ? "COMPLETE" : phase;
  const displayPhaseIndex = PHASE_ORDER.indexOf(displayPhase);

  const formatRunLabel = (run: WorkflowRun) => {
    const d = new Date(run.startedAt);
    return `${d.toLocaleDateString("en-GB", { month: "short", day: "numeric" })} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  };

  const formatRunTrigger = (type: string) => type.split(".").filter(Boolean).pop() ?? type;

  return (
    <div className={styles.container}>
      <PageHeader
        icon={<Orbit size={20} className={styles.headerIcon} />}
        title="Orchestration"
        subtitle="Real-time multi-agent pipeline"
        right={
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
        }
      />

      <div className={styles.body}>
        <main className={styles.logPanel}>
          <div className={styles.logPanelHeader}>
            <span className={`${styles.logDot} ${isRunning ? styles.logDotActive : ""}`} />
            <span className={styles.logPanelTitle}>
              {selectedRun ? `Replay — ${formatRunTrigger(selectedRun.triggerType)}` : "Output"}
            </span>
            {displayPhase === "COMPLETE" && !isRunning && !viewingRun && (
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
            {displayLogs.length === 0 && !isRunning && !viewingRun && (
              <div className={styles.logIdle}>
                <Terminal size={32} className={styles.logIdleIcon} />
                <p>Select a workflow and execute to start the pipeline</p>
                <span>Output streams here in real time &bull; History tab shows past runs</span>
              </div>
            )}
            {viewingRun && runLoading && (
              <div className={styles.logIdle}>
                <Loader size={28} className={`${styles.logIdleIcon} ${styles.spinIcon}`} />
                <p>Loading run…</p>
              </div>
            )}
            {viewingRun && !runLoading && (runError || !selectedRun) && (
              <div className={styles.logIdle}>
                <History size={28} className={styles.logIdleIcon} />
                <p>Run not found</p>
                <span>It may have been removed or never existed</span>
              </div>
            )}
            {displayLogs.length === 0 && viewingRun && !runLoading && selectedRun && (
              <div className={styles.logIdle}>
                <History size={28} className={styles.logIdleIcon} />
                <p>No log entries recorded for this run</p>
              </div>
            )}
            {displayLogs.map((log, i) => {
              const entryId = `${runId ?? "live"}-${i}`;
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

        <ExecuteSidebar
          sidebarView={sidebarView}
          executeMode={executeMode}
          workflows={workflows}
          workspaces={workspaces}
          workflowRuns={workflowRuns}
          selectedWorkflow={selectedWorkflow}
          selectedWorkspaceId={selectedWorkspaceId}
          scenarioInput={scenarioInput}
          activeRunId={runId}
          isRunning={isRunning}
          phase={phase}
          onSetSidebarView={setSidebarView}
          onSetExecuteMode={setExecuteMode}
          onSelectWorkflow={setSelectedWorkflow}
          onSetWorkspaceId={setSelectedWorkspaceId}
          onSetScenarioInput={setScenarioInput}
          onRunWorkflow={runWorkflow}
          onRunDynamic={runDynamic}
          onLoadRun={loadRun}
          formatRunLabel={formatRunLabel}
          formatRunTrigger={formatRunTrigger}
        />
      </div>
    </div>
  );
};

export default AgentOrchestration;
