import { ShieldOff } from "lucide-react";
import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";

interface RunSummaryProps {
  entry: SandboxExecutionLog;
}

const RunSummary = ({ entry }: RunSummaryProps) => (
  <div className={styles.auditBody}>
    {entry.policyDenied && (
      <div className={styles.policyDeniedBanner}>
        <ShieldOff size={14} style={{ flexShrink: 0, marginTop: 1 }} />
        <span>
          <strong>Execution was denied by Sentinel PDP.</strong>
          {entry.policyDenialReason ? ` ${entry.policyDenialReason}` : ""}
        </span>
      </div>
    )}

    <dl className={styles.auditMeta}>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Agent ID</dt>
        <dd className={styles.metaValue} title={entry.agentId}>
          {entry.agentId}
        </dd>
      </div>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Language</dt>
        <dd className={styles.metaValue}>{entry.language}</dd>
      </div>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Job ID</dt>
        <dd className={styles.metaValue} title={entry.jobId}>
          {entry.jobId.slice(0, 8)}…
        </dd>
      </div>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Executed At</dt>
        <dd className={styles.metaValue}>{new Date(entry.executedAt).toLocaleString("en-GB")}</dd>
      </div>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Correlation ID</dt>
        <dd className={styles.metaValue} title={entry.correlationId}>
          {entry.correlationId.slice(0, 8)}…
        </dd>
      </div>
      <div className={styles.metaItem}>
        <dt className={styles.metaLabel}>Sentinel Audit ID</dt>
        <dd className={styles.metaValue} title={entry.sentinelAuditId}>
          {entry.sentinelAuditId.slice(0, 8)}…
        </dd>
      </div>
      {entry.originalUserPrompt && (
        <div className={styles.metaItem} style={{ gridColumn: "1 / -1" }}>
          <dt className={styles.metaLabel}>User Prompt</dt>
          <dd className={styles.metaValueFull}>{entry.originalUserPrompt}</dd>
        </div>
      )}
    </dl>
  </div>
);

export default RunSummary;
