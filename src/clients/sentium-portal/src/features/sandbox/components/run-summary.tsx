import { ShieldOff } from "lucide-react";
import styles from "../sandbox.module.css";
import { StatusMessage } from "@/shared/ui/status-message";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";

interface RunSummaryProps {
  entry: SandboxExecutionLog;
}

export function RunSummary({ entry }: RunSummaryProps) {
  const items: { label: string; value: string; title?: string }[] = [
    { label: "Agent ID", value: entry.agentId, title: entry.agentId },
    { label: "Language", value: entry.language },
    { label: "Job ID", value: `${entry.jobId.slice(0, 8)}…`, title: entry.jobId },
    { label: "Executed At", value: new Date(entry.executedAt).toLocaleString("en-GB") },
    { label: "Correlation ID", value: `${entry.correlationId.slice(0, 8)}…`, title: entry.correlationId },
    { label: "Sentinel Audit ID", value: `${entry.sentinelAuditId.slice(0, 8)}…`, title: entry.sentinelAuditId },
  ];

  return (
    <div className={styles.section}>
      {entry.policyDenied && (
        <StatusMessage
          variant="warning"
          icon={<ShieldOff />}
          message={
            <>
              <strong>Execution was denied by Sentinel PDP.</strong>
              {entry.policyDenialReason ? ` ${entry.policyDenialReason}` : ""}
            </>
          }
        />
      )}

      <dl className={styles.meta}>
        {items.map((item) => (
          <div key={item.label} className={styles.metaItem}>
            <dt>{item.label}</dt>
            <dd title={item.title}>{item.value}</dd>
          </div>
        ))}
        {entry.originalUserPrompt && (
          <div className={`${styles.metaItem} ${styles.metaWide}`}>
            <dt>User Prompt</dt>
            <dd className={styles.prompt}>{entry.originalUserPrompt}</dd>
          </div>
        )}
      </dl>
    </div>
  );
}
