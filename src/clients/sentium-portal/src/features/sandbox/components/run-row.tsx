import { AlertTriangle, ChevronRight, File } from "lucide-react";
import styles from "../sandbox.module.css";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";
import { formatDateTimeShort, formatRelativeTime } from "../../../shared/utils/formatters";
import { RunStatusBadge } from "./run-status-badge";

interface RunRowProps {
  run: SandboxExecutionLog;
  onOpen: (jobId: string) => void;
}

export function RunRow({ run, onOpen }: RunRowProps) {
  return (
    <tr
      className={styles.runRow}
      tabIndex={0}
      onClick={() => onOpen(run.jobId)}
      onKeyDown={(e) => e.key === "Enter" && onOpen(run.jobId)}
    >
      <td>
        <span className={styles.statusCell}>
          <RunStatusBadge entry={run} />
          {run.timedOut && (
            <span className={styles.timeout} title="Execution timed out">
              <AlertTriangle size={13} />
            </span>
          )}
        </span>
      </td>
      <td className={styles.agent} title={run.agentId}>
        {run.agentId}
      </td>
      <td>
        <span className={styles.lang}>{run.language}</span>
      </td>
      <td title={formatDateTimeShort(run.executedAt)}>{formatRelativeTime(run.executedAt)}</td>
      <td className="num">{run.durationMs.toLocaleString()} ms</td>
      <td className={styles.mono}>exit {run.exitCode}</td>
      <td className="num">
        <span className={styles.artifacts}>
          <File size={13} aria-hidden="true" />
          {run.artifacts.length}
        </span>
      </td>
      <td className={styles.chevronCell}>
        <ChevronRight size={16} aria-hidden="true" />
      </td>
    </tr>
  );
}
