import { FolderOpen, GitBranch, History, Layers, Play, Sparkles, Square } from "lucide-react";
import styles from "../agent-orchestration.module.css";
import { Button } from "@/shared/ui/button";
import { Select, Textarea } from "@/shared/ui/form-controls";
import { InfiniteScrollSentinel } from "@/shared/ui/infinite-scroll-sentinel";
import type { WorkflowRecord } from "@/features/workflows/types/workflows";
import type { WorkflowRun } from "@/features/workflows/types/workflows";
import type { Phase } from "@/features/orchestration/types/orchestration";
import type { RunConnection } from "@/features/orchestration/state/orchestration-run-store";

interface Workspace {
  id: string;
  name: string;
  fileCount: number;
}

export type ExecuteMode = "predefined" | "dynamic";

interface ExecuteSidebarProps {
  sidebarView: "execute" | "history";
  executeMode: ExecuteMode;
  workflows: WorkflowRecord[];
  workspaces: Workspace[];
  workflowRuns: WorkflowRun[];
  hasMoreRuns: boolean;
  isLoadingMoreRuns: boolean;
  onLoadMoreRuns: () => void;
  selectedWorkflow: WorkflowRecord | null;
  selectedWorkspaceId: string;
  scenarioInput: string;
  activeRunId?: string;
  isRunning: boolean;
  connection: RunConnection;
  phase: Phase;
  onSetSidebarView: (view: "execute" | "history") => void;
  onSetExecuteMode: (mode: ExecuteMode) => void;
  onSelectWorkflow: (wf: WorkflowRecord) => void;
  onSetWorkspaceId: (id: string) => void;
  onSetScenarioInput: (value: string) => void;
  onRunWorkflow: () => void;
  onRunDynamic: () => void;
  onStopRun: () => void;
  onLoadRun: (run: WorkflowRun) => void;
  formatRunLabel: (run: WorkflowRun) => string;
  formatRunTrigger: (type: string) => string;
}

interface SegmentOption<T extends string> {
  value: T;
  label: string;
  icon: React.ReactNode;
}

/** Two-way toggle rendered as pressed buttons (the panel switch, not a tab set). */
function Segmented<T extends string>({
  options,
  value,
  onChange,
  disabled,
  label,
}: {
  options: SegmentOption<T>[];
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
  label: string;
}) {
  return (
    <div className={styles.segmented} role="group" aria-label={label}>
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          className={styles.segment}
          aria-pressed={value === o.value}
          onClick={() => onChange(o.value)}
          disabled={disabled}
        >
          {o.icon}
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function ExecuteSidebar({
  sidebarView,
  executeMode,
  workflows,
  workspaces,
  workflowRuns,
  hasMoreRuns,
  isLoadingMoreRuns,
  onLoadMoreRuns,
  selectedWorkflow,
  selectedWorkspaceId,
  scenarioInput,
  activeRunId,
  isRunning,
  connection,
  onSetSidebarView,
  onSetExecuteMode,
  onSelectWorkflow,
  onSetWorkspaceId,
  onSetScenarioInput,
  onRunWorkflow,
  onRunDynamic,
  onStopRun,
  onLoadRun,
  formatRunLabel,
  formatRunTrigger,
}: ExecuteSidebarProps) {
  const isStopping = connection === "stopping";

  const statusLabel = !isRunning
    ? "Idle"
    : connection === "starting"
      ? "Queued"
      : connection === "connecting"
        ? "Connecting"
        : connection === "waiting"
          ? "Warming up"
          : connection === "stopping"
            ? "Stopping"
            : "Streaming";

  const stopButton = (
    <Button
      variant="danger"
      fullWidth
      icon={<Square size={12} fill="currentColor" />}
      onClick={onStopRun}
      disabled={isStopping}
    >
      {isStopping ? "Stopping…" : "Stop run"}
    </Button>
  );

  const workspaceField = (
    <div className={styles.field}>
      <label className={styles.fieldLabel} htmlFor="orchestration-workspace">
        Workspace <span className={styles.optional}>optional</span>
      </label>
      <Select
        id="orchestration-workspace"
        value={selectedWorkspaceId}
        onChange={(e) => onSetWorkspaceId(e.target.value)}
        disabled={isRunning}
      >
        <option value="">No workspace</option>
        {workspaces.map((ws) => (
          <option key={ws.id} value={ws.id}>
            {ws.name}
            {ws.fileCount > 0 ? ` (${ws.fileCount} files)` : ""}
          </option>
        ))}
      </Select>
      {selectedWorkspaceId && (
        <p className={styles.fieldHint}>
          <FolderOpen size={12} aria-hidden="true" />
          Agents can read and write files in this workspace
        </p>
      )}
    </div>
  );

  return (
    <aside className={styles.sidebar} aria-label="Run controls">
      <div className={styles.sidebarTop}>
        <Segmented
          label="Panel"
          value={sidebarView}
          onChange={onSetSidebarView}
          options={[
            { value: "execute", label: "Execute", icon: <Play size={13} aria-hidden="true" /> },
            { value: "history", label: "History", icon: <History size={13} aria-hidden="true" /> },
          ]}
        />
      </div>

      {sidebarView === "execute" && (
        <div className={styles.sidebarScroll}>
          <Segmented
            label="Run mode"
            value={executeMode}
            onChange={onSetExecuteMode}
            disabled={isRunning}
            options={[
              { value: "predefined", label: "Predefined", icon: <Layers size={13} aria-hidden="true" /> },
              { value: "dynamic", label: "Dynamic", icon: <Sparkles size={13} aria-hidden="true" /> },
            ]}
          />

          {executeMode === "predefined" && (
            <>
              <div className={styles.field}>
                <span className={styles.fieldLabel}>
                  Workflows <span className={styles.count}>{workflows.length}</span>
                </span>
                <div className={styles.workflowList}>
                  {workflows.length === 0 ? (
                    <span className={styles.empty}>No workflows defined</span>
                  ) : (
                    workflows.map((wf) => (
                      <button
                        key={wf.id}
                        type="button"
                        className={styles.workflowItem}
                        aria-pressed={selectedWorkflow?.id === wf.id}
                        onClick={() => onSelectWorkflow(wf)}
                        disabled={isRunning}
                      >
                        <GitBranch size={14} className={styles.workflowIcon} aria-hidden="true" />
                        <span className={styles.workflowInfo}>
                          <span className={styles.workflowName}>{wf.name}</span>
                          <span className={styles.workflowMeta}>
                            {wf.agents.length} agent{wf.agents.length !== 1 ? "s" : ""}
                          </span>
                        </span>
                      </button>
                    ))
                  )}
                </div>
              </div>

              {selectedWorkflow && (
                <>
                  {workspaceField}
                  <div className={styles.field}>
                    <label className={styles.fieldLabel} htmlFor="orchestration-scenario">
                      Run: {selectedWorkflow.name}
                    </label>
                    <Textarea
                      id="orchestration-scenario"
                      value={scenarioInput}
                      onChange={(e) => onSetScenarioInput(e.target.value)}
                      placeholder="Describe the scenario for this workflow..."
                      rows={3}
                      disabled={isRunning}
                    />
                  </div>
                  {isRunning ? (
                    stopButton
                  ) : (
                    <Button variant="primary" fullWidth icon={<Play size={15} />} onClick={onRunWorkflow}>
                      Execute Workflow
                    </Button>
                  )}
                </>
              )}
            </>
          )}

          {executeMode === "dynamic" && (
            <>
              <div className={styles.dynamicNote}>
                <Sparkles size={16} aria-hidden="true" />
                <span>
                  <strong>Dynamic run.</strong> Sentium plans and assembles the right agents for your scenario.
                </span>
              </div>
              {workspaceField}
              <div className={styles.field}>
                <label className={styles.fieldLabel} htmlFor="orchestration-dynamic-scenario">
                  Scenario
                </label>
                <Textarea
                  id="orchestration-dynamic-scenario"
                  value={scenarioInput}
                  onChange={(e) => onSetScenarioInput(e.target.value)}
                  placeholder="Describe what you want Sentium to investigate or accomplish..."
                  rows={4}
                  disabled={isRunning}
                />
              </div>
              {isRunning ? (
                stopButton
              ) : (
                <Button
                  variant="primary"
                  fullWidth
                  icon={<Sparkles size={15} />}
                  onClick={onRunDynamic}
                  disabled={!scenarioInput.trim()}
                >
                  Run Dynamic
                </Button>
              )}
            </>
          )}
        </div>
      )}

      {sidebarView === "history" && (
        <div className={styles.sidebarScroll}>
          <span className={styles.fieldLabel}>
            Recent Runs <span className={styles.count}>{workflowRuns.length}</span>
          </span>
          <div className={styles.runList}>
            {workflowRuns.length === 0 && <div className={styles.empty}>No runs recorded yet</div>}
            {workflowRuns.map((run) => (
              <button
                key={run.id}
                type="button"
                className={styles.runItem}
                aria-current={activeRunId === run.id || undefined}
                onClick={() => onLoadRun(run)}
              >
                <span className={styles.runItemTop}>
                  <span className={styles.runTrigger}>{formatRunTrigger(run.triggerType)}</span>
                  <span className={styles.runEntries}>{run.logCount ?? run.logs?.length ?? 0} entries</span>
                </span>
                <span className={styles.runDate}>{formatRunLabel(run)}</span>
              </button>
            ))}
            <InfiniteScrollSentinel hasMore={hasMoreRuns} isLoading={isLoadingMoreRuns} onLoadMore={onLoadMoreRuns} />
          </div>
        </div>
      )}

      <dl className={styles.connection}>
        <div>
          <dt>Protocol</dt>
          <dd>SSE</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd data-active={isRunning || undefined}>{statusLabel}</dd>
        </div>
      </dl>
    </aside>
  );
}
