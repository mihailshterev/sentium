import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";

interface TerminalOutputProps {
  entry: SandboxExecutionLog;
}

const TerminalOutput = ({ entry }: TerminalOutputProps) => {
  const stdoutLines = entry.output ? entry.output.split("\n") : [];
  const stderrLines = entry.error ? entry.error.split("\n") : [];
  const hasStdout = stdoutLines.some((l) => l.trim());
  const hasStderr = stderrLines.some((l) => l.trim());

  return (
    <div className={styles.terminalWrap}>
      <div className={styles.terminalHeader}>
        <div className={styles.termDots}>
          <span className={styles.termDot} />
          <span className={styles.termDot} />
          <span className={styles.termDot} />
        </div>
        stdout / stderr
      </div>
      <div className={styles.terminal}>
        {!hasStdout && !hasStderr && <span className={styles.termEmpty}>(no output)</span>}
        {hasStdout &&
          stdoutLines.map((line, i) => (
            <span key={`out-${i}`} className={styles.termLineStdout}>
              {line || "\u00A0"}
            </span>
          ))}
        {hasStderr && (
          <>
            {hasStdout && <span className={styles.termSectionLabel}>── stderr ──</span>}
            {stderrLines.map((line, i) => (
              <span key={`err-${i}`} className={styles.termLineStderr}>
                {line || "\u00A0"}
              </span>
            ))}
          </>
        )}
      </div>
    </div>
  );
};

export default TerminalOutput;
