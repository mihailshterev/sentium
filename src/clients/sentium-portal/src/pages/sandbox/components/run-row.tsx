import { AlertTriangle, ChevronRight, File } from "lucide-react";
import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";
import { formatDateTimeShort, formatRelativeTime } from "../../../utils/formatters";
import RunStatusBadge from "./run-status-badge";

interface RunRowProps {
  run: SandboxExecutionLog;
  onOpen: (jobId: string) => void;
}

const RunRow = ({ run, onOpen }: RunRowProps) => (
  <div
    className={styles.runRow}
    role="button"
    tabIndex={0}
    onClick={() => onOpen(run.jobId)}
    onKeyDown={(e) => e.key === "Enter" && onOpen(run.jobId)}
  >
    <span className={styles.runStatusCell}>
      <RunStatusBadge entry={run} />
      {run.timedOut && (
        <span className={styles.runTimeout} title="Execution timed out">
          <AlertTriangle size={11} />
        </span>
      )}
    </span>
    <span className={styles.runAgent} title={run.agentId}>
      {run.agentId}
    </span>
    <span className={`${styles.langChip} ${run.language === "Python" ? styles.langPython : styles.langNode}`}>
      {run.language}
    </span>
    <span className={styles.runStarted} title={formatDateTimeShort(run.executedAt)}>
      {formatRelativeTime(run.executedAt)}
    </span>
    <span className={styles.runDuration}>{run.durationMs.toLocaleString()} ms</span>
    <span className={styles.runExit}>exit {run.exitCode}</span>
    <span className={styles.runArtifacts}>
      <File size={12} />
      {run.artifacts.length}
    </span>
    <ChevronRight size={15} className={styles.runChevron} />
  </div>
);

export default RunRow;
