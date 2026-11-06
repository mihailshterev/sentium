import { FileCode } from "lucide-react";
import styles from "../sandbox.module.css";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";

interface RunScriptProps {
  entry: SandboxExecutionLog;
}

export function RunScript({ entry }: RunScriptProps) {
  return (
    <div className={styles.section}>
      <div>
        <p className={styles.sectionLabel}>Source code</p>
        <pre className={styles.code}>
          <code>{entry.code}</code>
        </pre>
      </div>

      {entry.fileContext.length > 0 && (
        <div className={styles.section}>
          <p className={styles.sectionLabel}>File Context ({entry.fileContext.length})</p>
          {entry.fileContext.map((f) => (
            <div key={f.fileName} className={styles.fileContext}>
              <div className={styles.fileContextName}>
                <FileCode size={13} aria-hidden="true" />
                {f.fileName}
              </div>
              <pre className={styles.code}>
                <code>{f.content}</code>
              </pre>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
