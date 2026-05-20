import { AlertTriangle, CheckCircle2, Clock, Cpu, Hash, MemoryStick, ShieldOff, XCircle } from "lucide-react";
import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";

interface MetadataBadgesProps {
  entry: SandboxExecutionLog;
}

const MetadataBadges = ({ entry }: MetadataBadgesProps) => (
  <div className={styles.badges}>
    {entry.policyDenied ? (
      <span className={`${styles.badge} ${styles.badgeDenied}`}>
        <ShieldOff size={11} /> Policy Denied
      </span>
    ) : entry.succeeded ? (
      <span className={`${styles.badge} ${styles.badgeSuccess}`}>
        <CheckCircle2 size={11} /> Succeeded
      </span>
    ) : (
      <span className={`${styles.badge} ${styles.badgeFail}`}>
        <XCircle size={11} /> Failed
      </span>
    )}
    {entry.timedOut && (
      <span className={`${styles.badge} ${styles.badgeTimeout}`}>
        <AlertTriangle size={11} /> Timed Out
      </span>
    )}
    <span className={`${styles.badge} ${styles.badgeExit}`}>
      <Hash size={11} /> exit {entry.exitCode}
    </span>
    <span className={`${styles.badge} ${styles.badgeDuration}`}>
      <Clock size={11} /> {entry.durationMs.toLocaleString()} ms
    </span>
    <span className={`${styles.badge} ${styles.badgeMemory}`}>
      <MemoryStick size={11} /> 256 MB
    </span>
    <span className={`${styles.badge} ${styles.badgeCpu}`}>
      <Cpu size={11} /> 0.5 vCPU
    </span>
  </div>
);

export default MetadataBadges;
