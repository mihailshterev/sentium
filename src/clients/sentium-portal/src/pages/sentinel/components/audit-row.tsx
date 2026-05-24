import { ChevronDown, ChevronUp } from "lucide-react";
import styles from "../sentinel.module.scss";
import type { AuditRecord } from "../../../types/sentinel";
import { formatTimeHms } from "../../../utils/formatters";
import RiskBadge from "./risk-badge";
import EffectBadge from "./effect-badge";
import AlignmentBadge from "./alignment-badge";

interface AuditRowProps {
  record: AuditRecord;
  expanded: boolean;
  onToggle: () => void;
}

const AuditRow = ({ record, expanded, onToggle }: AuditRowProps) => (
  <>
    <div
      className={`${styles.auditRow} ${!record.allowed ? styles.auditRowDenied : ""} ${expanded ? styles.auditRowExpanded : ""}`}
      onClick={onToggle}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === "Enter" && onToggle()}
    >
      <span className={styles.auditTime}>{formatTimeHms(record.timestamp)}</span>
      <span className={styles.auditAgent} title={record.agentId}>
        {record.agentId}
      </span>
      <span className={styles.auditSkill} title={record.skillName}>
        {record.skillName || "—"}
      </span>
      <span className={styles.auditAction}>{record.action}</span>
      <EffectBadge allowed={record.allowed} effect={record.effect} />
      <RiskBadge risk={record.risk} />
      <AlignmentBadge verdict={record.alignmentVerdict} />
      <span className={styles.auditChevron}>{expanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}</span>
    </div>
    {expanded && (
      <div className={styles.auditDetail}>
        <div className={styles.auditDetailGrid}>
          <div>
            <span className={styles.auditDetailLabel}>Resource</span>
            <span className={styles.auditDetailValue}>
              {record.resourceType} / {record.resourceId}
            </span>
          </div>
          <div>
            <span className={styles.auditDetailLabel}>Policies Triggered</span>
            <span className={styles.auditDetailValue}>
              {record.triggeredPolicies.length > 0 ? record.triggeredPolicies.join(", ") : "None"}
            </span>
          </div>
          <div>
            <span className={styles.auditDetailLabel}>Eval Duration</span>
            <span className={styles.auditDetailValue}>{record.evaluationDurationMs}ms</span>
          </div>
          <div>
            <span className={styles.auditDetailLabel}>Correlation ID</span>
            <span className={`${styles.auditDetailValue} ${styles.mono}`}>{record.correlationId || "—"}</span>
          </div>
        </div>
        <div className={styles.auditReason}>
          <span className={styles.auditDetailLabel}>Reason</span>
          <p className={styles.auditReasonText}>{record.reason}</p>
        </div>
      </div>
    )}
  </>
);

export default AuditRow;
