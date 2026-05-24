import { useState, useEffect, useCallback, useRef } from "react";
import { Zap, CheckCircle, Circle, Loader, Terminal, History, Orbit } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import styles from "./agent-orchestration.module.scss";
import { runWorkflowPipeline, fetchWorkflowRuns, fetchWorkspaces } from "../../services/agentRuntime.service";
import useWorkflows from "../../hooks/useWorkflows";
import type { Phase, LogEntry } from "../../types/orchestration";
import type { WorkflowRecord } from "../../types/workflows";
import type { WorkflowRun } from "../../types/workflows";
import { BASE_URL } from "../../api/client";
import PageHeader from "../../components/ui/page-header";
import LogEntryView from "./components/log-entry-view";
import ExecuteSidebar from "./components/execute-sidebar";

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

        <ExecuteSidebar
          sidebarView={sidebarView}
          workflows={workflows}
          workspaces={workspaces}
          workflowRuns={workflowRuns}
          selectedWorkflow={selectedWorkflow}
          selectedWorkspaceId={selectedWorkspaceId}
          scenarioInput={scenarioInput}
          selectedRun={selectedRun}
          isRunning={isRunning}
          phase={phase}
          onSetSidebarView={setSidebarView}
          onSelectWorkflow={setSelectedWorkflow}
          onSetWorkspaceId={setSelectedWorkspaceId}
          onSetScenarioInput={setScenarioInput}
          onRunWorkflow={runWorkflow}
          onLoadRun={loadRun}
          formatRunLabel={formatRunLabel}
          formatRunTrigger={formatRunTrigger}
        />
      </div>
    </div>
  );
};

export default AgentOrchestration;
