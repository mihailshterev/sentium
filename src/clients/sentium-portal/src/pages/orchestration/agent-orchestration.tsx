import { useState, useRef, useEffect } from "react";
import { useParams, useNavigate } from "react-router";
import { Zap, CheckCircle, Circle, Loader, Terminal, History, Orbit, Square } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import styles from "./agent-orchestration.module.scss";
import { fetchWorkflowRunsPaged, fetchWorkspaces } from "../../services/agentRuntime.service";
import useWorkflows from "../../hooks/useWorkflows";
import { useWorkflowRun } from "../../hooks/useWorkflowRuns";
import { useInfiniteList } from "../../hooks/useInfiniteList";
import type { Phase, LogEntry } from "../../types/orchestration";
import type { WorkflowRecord } from "../../types/workflows";
import type { WorkflowRun } from "../../types/workflows";
import { useOrchestrationRunStore } from "../../stores/orchestration-run-store";
import PageHeader from "../../components/ui/page-header";
import LogEntryView from "./components/log-entry-view";
import ExecuteSidebar, { type ExecuteMode } from "./components/execute-sidebar";

const ALL_PHASE_STEPS: { key: Phase; label: string; icon: React.ElementType }[] = [
  { key: "PLANNING", label: "Plan", icon: Circle },
  { key: "SQUAD", label: "Execute", icon: Zap },
  { key: "VALIDATING", label: "Validate", icon: CheckCircle },
];

const PREDEFINED_PHASE_STEPS: { key: Phase; label: string; icon: React.ElementType }[] = [
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

  const {
    logs,
    phase,
    isRunning: storeRunning,
    isDynamicRun,
    connection,
    runStartedAt,
    lastOutcome,
    startPredefined,
    startDynamic,
    stopRun,
  } = useOrchestrationRunStore();

  const [sidebarView, setSidebarView] = useState<"execute" | "history">(runId ? "history" : "execute");
  const [executeMode, setExecuteMode] = useState<ExecuteMode>(() => (isDynamicRun ? "dynamic" : "predefined"));
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowRecord | null>(null);
  const [scenarioInput, setScenarioInput] = useState("");
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string>("");
  const [expandedThoughts, setExpandedThoughts] = useState<Set<string>>(new Set());

  const scrollRef = useRef<HTMLDivElement>(null);

  const { run: selectedRun, isLoading: runLoading, error: runError } = useWorkflowRun(runId);

  const { data: workspaces = [] } = useQuery({
    queryKey: ["workspaces", "options"],
    queryFn: fetchWorkspaces,
  });

  const {
    items: workflowRuns,
    refetch: refetchRuns,
    hasMore: hasMoreRuns,
    loadMore: loadMoreRuns,
    isLoadingMore: isLoadingMoreRuns,
  } = useInfiniteList<WorkflowRun>(["workflowRuns"], fetchWorkflowRunsPaged, {
    enabled: sidebarView === "history",
    refetchInterval: sidebarView === "history" ? 15_000 : undefined,
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

  const wasRunning = useRef(false);
  useEffect(() => {
    if (wasRunning.current && !storeRunning && phase === "COMPLETE") {
      void refetchRuns();
    }
    wasRunning.current = storeRunning;
  }, [storeRunning, phase, refetchRuns]);

  const [elapsedMs, setElapsedMs] = useState(0);
  useEffect(() => {
    if (!storeRunning || runStartedAt == null) {
      return;
    }
    const id = setInterval(() => setElapsedMs(Date.now() - runStartedAt), 1000);
    return () => {
      clearInterval(id);
      setElapsedMs(0);
    };
  }, [storeRunning, runStartedAt]);

  const leaveReplay = () => {
    setSidebarView("execute");
    if (runId) {
      navigate("/orchestration");
    }
  };

  const runWorkflow = async () => {
    if (!selectedWorkflow) {
      return;
    }
    const scenario = scenarioInput.trim() || `Execute workflow: ${selectedWorkflow.name}`;
    leaveReplay();
    await startPredefined({
      workflowId: selectedWorkflow.id,
      scenario,
      ...(selectedWorkspaceId && { workspaceId: selectedWorkspaceId }),
    });
  };

  const runDynamic = async () => {
    const scenario = scenarioInput.trim();
    if (!scenario) {
      return;
    }
    leaveReplay();
    await startDynamic({
      activity: scenario,
      ...(selectedWorkspaceId && { workspaceId: selectedWorkspaceId }),
    });
  };

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
    if (a.includes("orchestrator") || a.includes("planner")) return "rolePlanner";
    if (a.includes("validator")) return "roleValidator";
    return "roleSquad";
  };

  const viewingRun = !!runId;
  const isRunning = !viewingRun && storeRunning;
  const displayLogs = viewingRun && selectedRun ? coalesceLog(selectedRun.logs ?? []) : logs;
  const displayPhase: Phase = viewingRun ? "COMPLETE" : phase;
  const displayPhaseIndex = PHASE_ORDER.indexOf(displayPhase);
  const isDynamicPhase = storeRunning ? isDynamicRun : executeMode === "dynamic";
  const phaseSteps = !viewingRun && !isDynamicPhase ? PREDEFINED_PHASE_STEPS : ALL_PHASE_STEPS;

  const formatRunLabel = (run: WorkflowRun) => {
    const d = new Date(run.startedAt);
    return `${d.toLocaleDateString("en-GB", { month: "short", day: "numeric" })} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  };

  const formatRunTrigger = (type: string) => type.split(".").filter(Boolean).pop() ?? type;

  const formatElapsed = (ms: number) => {
    const total = Math.max(0, Math.floor(ms / 1000));
    return `${Math.floor(total / 60)}:${(total % 60).toString().padStart(2, "0")}`;
  };

  const renderRunIndicator = () => {
    if (connection === "streaming") {
      return (
        <div className={styles.streamingIndicator}>
          <span className={styles.cursor} />
          <span className={styles.streamingLabel}>Streaming…</span>
        </div>
      );
    }

    if (connection === "stopping") {
      return (
        <div className={styles.streamingIndicator}>
          <Loader size={13} className={styles.spinIcon} />
          <span className={styles.streamingLabel}>Stopping…</span>
        </div>
      );
    }

    const label =
      connection === "starting" ? "Queued…" : connection === "connecting" ? "Connecting…" : "Warming up agents";

    return (
      <div className={styles.streamingIndicator}>
        <span className={styles.thinkingDots}>
          <span className={styles.thinkingDot} />
          <span className={styles.thinkingDot} />
          <span className={styles.thinkingDot} />
        </span>
        <span className={styles.streamingLabel}>{label}</span>
        {connection === "waiting" && (
          <>
            <span className={styles.streamingHint}>local models may take a moment</span>
            <span className={styles.streamingElapsed}>{formatElapsed(elapsedMs)}</span>
          </>
        )}
      </div>
    );
  };

  return (
    <div className={styles.container}>
      <PageHeader
        icon={<Orbit size={20} className={styles.headerIcon} />}
        title="Orchestration"
        subtitle="Real-time multi-agent pipeline"
        right={
          <div className={styles.phaseBar}>
            {phaseSteps.map((step, i) => {
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
                  {i < phaseSteps.length - 1 && (
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
              {selectedRun ? `Replay - ${formatRunTrigger(selectedRun.triggerType)}` : "Output"}
            </span>
            {displayPhase === "COMPLETE" && !isRunning && !viewingRun && lastOutcome === "completed" && (
              <span className={styles.completeBadge}>
                <CheckCircle size={11} /> Complete
              </span>
            )}
            {displayPhase === "COMPLETE" && !isRunning && !viewingRun && lastOutcome === "stopped" && (
              <span className={styles.stoppedBadge}>
                <Square size={10} fill="currentColor" /> Stopped
              </span>
            )}
            {selectedRun && (
              <span className={styles.historyBadge}>
                <History size={10} /> {formatRunLabel(selectedRun)}
              </span>
            )}
            {isRunning && (
              <button
                type="button"
                className={styles.stopBtn}
                onClick={stopRun}
                disabled={connection === "stopping"}
                title="Stop run"
              >
                <Square size={12} fill="currentColor" />
                {connection === "stopping" ? "Stopping…" : "Stop"}
              </button>
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
              const isActiveThought = isRunning && log.type === "thought" && i === displayLogs.length - 1;
              return (
                <LogEntryView
                  key={entryId}
                  log={log}
                  entryId={entryId}
                  expanded={expandedThoughts.has(entryId)}
                  onToggle={toggleThought}
                  getRoleClass={getRoleClass}
                  isActiveThought={isActiveThought}
                />
              );
            })}
            {isRunning && renderRunIndicator()}
          </div>
        </main>

        <ExecuteSidebar
          sidebarView={sidebarView}
          executeMode={executeMode}
          workflows={workflows}
          workspaces={workspaces}
          workflowRuns={workflowRuns}
          hasMoreRuns={hasMoreRuns}
          isLoadingMoreRuns={isLoadingMoreRuns}
          onLoadMoreRuns={loadMoreRuns}
          selectedWorkflow={selectedWorkflow}
          selectedWorkspaceId={selectedWorkspaceId}
          scenarioInput={scenarioInput}
          activeRunId={runId}
          isRunning={isRunning}
          connection={connection}
          phase={phase}
          onSetSidebarView={setSidebarView}
          onSetExecuteMode={setExecuteMode}
          onSelectWorkflow={setSelectedWorkflow}
          onSetWorkspaceId={setSelectedWorkspaceId}
          onSetScenarioInput={setScenarioInput}
          onRunWorkflow={runWorkflow}
          onRunDynamic={runDynamic}
          onStopRun={stopRun}
          onLoadRun={loadRun}
          formatRunLabel={formatRunLabel}
          formatRunTrigger={formatRunTrigger}
        />
      </div>
    </div>
  );
};

export default AgentOrchestration;
