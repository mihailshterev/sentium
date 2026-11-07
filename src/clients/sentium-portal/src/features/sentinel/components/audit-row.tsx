import { ChevronDown } from "lucide-react";
import styles from "../sentinel.module.css";
import type { AuditRecord } from "@/features/sentinel/types/sentinel";
import { formatTimeHms } from "../../../shared/utils/formatters";
import { AlignmentBadge } from "./alignment-badge";
import { EffectBadge } from "./effect-badge";
import { RiskBadge } from "./risk-badge";

interface AuditRowProps {
  record: AuditRecord;
  expanded: boolean;
  onToggle: () => void;
  /** Column count, for the detail row's colspan. */
  columns?: number;
}

export function AuditRow({ record, expanded, onToggle, columns = 8 }: AuditRowProps) {
  return (
    <>
      <tr
        className={styles.auditRow}
        data-denied={!record.allowed || undefined}
        data-expanded={expanded || undefined}
        onClick={onToggle}
        tabIndex={0}
        aria-expanded={expanded}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            onToggle();
          }
        }}
      >
        <td className={styles.time}>{formatTimeHms(record.timestamp)}</td>
        <td className={styles.truncate} title={record.agentId}>
          {record.agentId}
        </td>
        <td className={`${styles.truncate} ${styles.mono}`} title={record.skillName}>
          {record.skillName || "—"}
        </td>
        <td>{record.action}</td>
        <td>
          <EffectBadge allowed={record.allowed} effect={record.effect} />
        </td>
        <td>
          <RiskBadge risk={record.risk} />
        </td>
        <td>
          <AlignmentBadge verdict={record.alignmentVerdict} />
        </td>
        <td className={styles.chevronCell}>
          <ChevronDown size={15} className={expanded ? styles.chevronOpen : styles.chevron} aria-hidden="true" />
        </td>
      </tr>
      {expanded && (
        <tr className={styles.detailRow}>
          <td colSpan={columns}>
            <dl className={styles.detailGrid}>
              <div>
                <dt>Resource</dt>
                <dd>
                  {record.resourceType} / {record.resourceId}
                </dd>
              </div>
              <div>
                <dt>Policies triggered</dt>
                <dd>{record.triggeredPolicies.length > 0 ? record.triggeredPolicies.join(", ") : "None"}</dd>
              </div>
              <div>
                <dt>Evaluation</dt>
                <dd>{record.evaluationDurationMs}ms</dd>
              </div>
              <div>
                <dt>Correlation ID</dt>
                <dd className={styles.mono}>{record.correlationId || "—"}</dd>
              </div>
            </dl>
            <div className={styles.reason}>
              <span className={styles.reasonLabel}>Reason</span>
              <p className={styles.reasonText}>{record.reason}</p>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
