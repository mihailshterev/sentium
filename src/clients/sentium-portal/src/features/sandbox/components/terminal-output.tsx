import styles from "../sandbox.module.css";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";

interface TerminalOutputProps {
  entry: SandboxExecutionLog;
}

export function TerminalOutput({ entry }: TerminalOutputProps) {
  const stdoutLines = entry.output ? entry.output.split("\n") : [];
  const stderrLines = entry.error ? entry.error.split("\n") : [];
  const hasStdout = stdoutLines.some((l) => l.trim());
  const hasStderr = stderrLines.some((l) => l.trim());

  return (
    <div className={styles.terminal}>
      <div className={styles.terminalBar}>
        <span className={styles.terminalDots} aria-hidden="true">
          <span />
          <span />
          <span />
        </span>
        stdout / stderr
      </div>
      <div className={styles.terminalBody}>
        {!hasStdout && !hasStderr && <span className={styles.terminalEmpty}>(no output)</span>}
        {hasStdout &&
          stdoutLines.map((line, i) => (
            <span key={`out-${i}`} className={styles.stdout}>
              {line || " "}
            </span>
          ))}
        {hasStderr && (
          <>
            {hasStdout && <span className={styles.streamLabel}>stderr</span>}
            {stderrLines.map((line, i) => (
              <span key={`err-${i}`} className={styles.stderr}>
                {line || " "}
              </span>
            ))}
          </>
        )}
      </div>
    </div>
  );
}
