import { useState, useRef, useEffect } from "react";
import { useParams, useNavigate } from "react-router";
import { CheckCircle, Circle, History, Loader2, Square, Terminal, Zap } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import styles from "../agent-orchestration.module.css";
import { fetchWorkflowRunsPaged } from "@/features/workflows/api/workflows.service";
import { fetchWorkspaces } from "@/features/workspaces/api/workspaces.service";
import { useWorkflows } from "@/features/workflows/hooks/useWorkflows";
import { useRenameWorkflowRun, useWorkflowRun } from "@/features/workflows/hooks/useWorkflowRuns";
import { describeRun } from "@/features/workflows/utils/describe-run";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import { useDebouncedValue } from "@/shared/hooks/useDebouncedValue";
import type { Phase, LogEntry } from "@/features/orchestration/types/orchestration";
import type { WorkflowRecord } from "@/features/workflows/types/workflows";
import type { WorkflowRun } from "@/features/workflows/types/workflows";
import { useOrchestrationRunStore } from "@/features/orchestration/state/orchestration-run-store";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { LogEntryView } from "../components/log-entry-view";
import { ExecuteSidebar, type ExecuteMode } from "../components/execute-sidebar";

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

export function AgentOrchestrationView() {
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
  const { mutate: renameRun } = useRenameWorkflowRun();

  const { data: workspaces = [] } = useQuery({
    queryKey: ["workspaces", "options"],
    queryFn: fetchWorkspaces,
  });

  const [runSearch, setRunSearch] = useState("");
  const debouncedRunSearch = useDebouncedValue(runSearch).trim();

  const {
    items: workflowRuns,
    totalCount: runCount,
    refetch: refetchRuns,
    hasMore: hasMoreRuns,
    loadMore: loadMoreRuns,
    isLoadingMore: isLoadingMoreRuns,
  } = useInfiniteList<WorkflowRun>(
    ["workflowRuns", debouncedRunSearch],
    (page, pageSize) => fetchWorkflowRunsPaged(page, pageSize, debouncedRunSearch || undefined),
    {
      enabled: sidebarView === "history",
      refetchInterval: sidebarView === "history" ? 15_000 : undefined,
      keepPrevious: true,
    },
  );

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

  const formatElapsed = (ms: number) => {
    const total = Math.max(0, Math.floor(ms / 1000));
    return `${Math.floor(total / 60)}:${(total % 60).toString().padStart(2, "0")}`;
  };

  const renderRunIndicator = () => {
    const label =
      connection === "streaming"
        ? "Streaming…"
        : connection === "stopping"
          ? "Stopping…"
          : connection === "starting"
            ? "Queued…"
            : connection === "connecting"
              ? "Connecting…"
              : "Warming up agents";

    return (
      <div className={styles.runIndicator} role="status">
        {connection === "stopping" ? (
          <Loader2 size={14} className="animate-spin" aria-hidden="true" />
        ) : (
          <span className={styles.dots} aria-hidden="true">
            <span />
            <span />
            <span />
          </span>
        )}
        <span className={styles.runIndicatorLabel}>{label}</span>
        {connection === "waiting" && (
          <>
            <span className={styles.runIndicatorHint}>local models may take a moment</span>
            <span className={styles.runIndicatorElapsed}>{formatElapsed(elapsedMs)}</span>
          </>
        )}
      </div>
    );
  };

  const phaseStepper = (
    <ol className={styles.phaseBar} aria-label="Pipeline phase">
      {phaseSteps.map((step) => {
        const stepIndex = PHASE_ORDER.indexOf(step.key);
        const isDone = displayPhaseIndex > stepIndex;
        const isActive = displayPhase === step.key;
        const Icon = isDone ? CheckCircle : isActive ? Loader2 : step.icon;
        return (
          <li
            key={step.key}
            className={styles.phaseStep}
            data-state={isDone ? "done" : isActive ? "active" : "pending"}
            aria-current={isActive ? "step" : undefined}
          >
            <span className={styles.phaseNode}>
              <Icon size={13} className={isActive ? "animate-spin" : undefined} aria-hidden="true" />
            </span>
            <span className={styles.phaseLabel}>{step.label}</span>
          </li>
        );
      })}
    </ol>
  );

  return (
    <Page
      title="Orchestration"
      subtitle="Real-time multi-agent pipeline"
      scroll={false}
      width="full"
      actions={phaseStepper}
    >
      <div className={styles.layout}>
        <section className={styles.logPanel} aria-label="Run output">
          <div className={styles.logHeader}>
            <span className={styles.logDot} data-active={isRunning || undefined} aria-hidden="true" />
            <span className={styles.logTitle}>
              {selectedRun ? `Replay - ${describeRun(selectedRun).title}` : "Output"}
            </span>
            {displayPhase === "COMPLETE" && !isRunning && !viewingRun && lastOutcome === "completed" && (
              <Badge tone="success">
                <CheckCircle size={12} aria-hidden="true" /> Complete
              </Badge>
            )}
            {displayPhase === "COMPLETE" && !isRunning && !viewingRun && lastOutcome === "stopped" && (
              <Badge tone="warning">
                <Square size={10} fill="currentColor" aria-hidden="true" /> Stopped
              </Badge>
            )}
            {selectedRun && (
              <Badge tone="neutral">
                <History size={12} aria-hidden="true" /> {formatRunLabel(selectedRun)}
              </Badge>
            )}
            {isRunning && (
              <Button
                variant="danger"
                size="sm"
                icon={<Square size={11} fill="currentColor" />}
                onClick={stopRun}
                disabled={connection === "stopping"}
                title="Stop run"
                className={styles.logStop}
              >
                {connection === "stopping" ? "Stopping…" : "Stop"}
              </Button>
            )}
          </div>

          <div className={styles.logWindow} ref={scrollRef}>
            {displayLogs.length === 0 && !isRunning && !viewingRun && (
              <EmptyState
                icon={<Terminal />}
                title="Select a workflow and execute to start the pipeline"
                hint="Output streams here in real time • History shows past runs"
              />
            )}
            {viewingRun && runLoading && (
              <div className={styles.logLoading} role="status">
                <Loader2 size={18} className="animate-spin" aria-hidden="true" />
                <span>Loading run…</span>
              </div>
            )}
            {viewingRun && !runLoading && (runError || !selectedRun) && (
              <EmptyState icon={<History />} title="Run not found" hint="It may have been removed or never existed" />
            )}
            {displayLogs.length === 0 && viewingRun && !runLoading && selectedRun && (
              <EmptyState icon={<History />} title="No log entries recorded for this run" />
            )}
            {displayLogs.length > 0 && (
              <ol className={styles.timeline}>
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
              </ol>
            )}
            {isRunning && renderRunIndicator()}
          </div>
        </section>

        <ExecuteSidebar
          sidebarView={sidebarView}
          executeMode={executeMode}
          workflows={workflows}
          workspaces={workspaces}
          workflowRuns={workflowRuns}
          runCount={runCount}
          runSearch={runSearch}
          onRunSearchChange={setRunSearch}
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
          onRenameRun={(id, title) => renameRun({ runId: id, title })}
          formatRunLabel={formatRunLabel}
        />
      </div>
    </Page>
  );
}
