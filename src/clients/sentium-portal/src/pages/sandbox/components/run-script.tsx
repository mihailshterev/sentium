import { FileCode } from "lucide-react";
import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";

interface RunScriptProps {
  entry: SandboxExecutionLog;
}

const RunScript = ({ entry }: RunScriptProps) => (
  <div className={styles.auditBody}>
    <div>
      <p className={styles.codeLabel}>Source Code</p>
      <div className={styles.codeViewer}>
        <pre>
          <code>{entry.code}</code>
        </pre>
      </div>
    </div>

    {entry.fileContext.length > 0 && (
      <>
        <hr className={styles.divider} />
        <div className={styles.fileContextSection}>
          <p className={styles.codeLabel}>File Context ({entry.fileContext.length})</p>
          {entry.fileContext.map((f) => (
            <div key={f.fileName} className={styles.fileContextItem}>
              <div className={styles.fileContextName}>
                <FileCode size={11} />
                {f.fileName}
              </div>
              <div className={styles.fileContextCode}>
                <pre>
                  <code>{f.content}</code>
                </pre>
              </div>
            </div>
          ))}
        </div>
      </>
    )}
  </div>
);

export default RunScript;
