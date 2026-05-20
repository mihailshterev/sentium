import { Play, GitBranch, FolderOpen, Loader, History } from "lucide-react";
import styles from "../agent-orchestration.module.scss";
import type { WorkflowRecord } from "../../../types/workflows";
import type { WorkflowRun } from "../../../types/workflowRuns";
import type { Phase } from "../../../types/orchestration";

interface Workspace {
  id: string;
  name: string;
  fileCount: number;
}

interface ExecuteSidebarProps {
  sidebarView: "execute" | "history";
  workflows: WorkflowRecord[];
  workspaces: Workspace[];
  workflowRuns: WorkflowRun[];
  selectedWorkflow: WorkflowRecord | null;
  selectedWorkspaceId: string;
  scenarioInput: string;
  selectedRun: WorkflowRun | null;
  isRunning: boolean;
  phase: Phase;
  onSetSidebarView: (view: "execute" | "history") => void;
  onSelectWorkflow: (wf: WorkflowRecord) => void;
  onSetWorkspaceId: (id: string) => void;
  onSetScenarioInput: (value: string) => void;
  onRunWorkflow: () => void;
  onLoadRun: (run: WorkflowRun) => void;
  formatRunLabel: (run: WorkflowRun) => string;
  formatRunTrigger: (type: string) => string;
}

const ExecuteSidebar = ({
  sidebarView,
  workflows,
  workspaces,
  workflowRuns,
  selectedWorkflow,
  selectedWorkspaceId,
  scenarioInput,
  selectedRun,
  isRunning,
  onSetSidebarView,
  onSelectWorkflow,
  onSetWorkspaceId,
  onSetScenarioInput,
  onRunWorkflow,
  onLoadRun,
  formatRunLabel,
  formatRunTrigger,
}: ExecuteSidebarProps) => {
  return (
    <aside className={styles.sidebar}>
      <div className={styles.sidebarTabs}>
        <button
          className={`${styles.sidebarTab} ${sidebarView === "execute" ? styles.sidebarTabActive : ""}`}
          onClick={() => onSetSidebarView("execute")}
        >
          <Play size={11} />
          Execute
        </button>
        <button
          className={`${styles.sidebarTab} ${sidebarView === "history" ? styles.sidebarTabActive : ""}`}
          onClick={() => onSetSidebarView("history")}
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
                    onClick={() => onSelectWorkflow(wf)}
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
                  onChange={(e) => onSetScenarioInput(e.target.value)}
                  placeholder="Describe the scenario for this workflow..."
                  rows={3}
                  disabled={isRunning}
                />
                <button className={styles.runWorkflowBtn} onClick={onRunWorkflow} disabled={isRunning}>
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
                onClick={() => onLoadRun(run)}
              >
                <div className={styles.runItemHeader}>
                  <span className={styles.runTrigger}>{formatRunTrigger(run.triggerType)}</span>
                  <span className={styles.runLogs}>{run.logs.length} entries</span>
                </div>
                <div className={styles.runMeta}>
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
  );
};

export default ExecuteSidebar;
