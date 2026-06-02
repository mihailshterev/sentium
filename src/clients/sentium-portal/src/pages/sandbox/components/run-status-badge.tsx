import { CheckCircle2, ShieldOff, XCircle } from "lucide-react";
import styles from "../sandbox.module.scss";
import type { SandboxExecutionLog } from "../../../types/sandbox";

type RunStatusKind = "succeeded" | "failed" | "denied";

function getRunStatus(entry: Pick<SandboxExecutionLog, "succeeded" | "policyDenied">): RunStatusKind {
  if (entry.policyDenied) {
    return "denied";
  }
  return entry.succeeded ? "succeeded" : "failed";
}

const CONFIG = {
  succeeded: { label: "Succeeded", cls: styles.statusOk, Icon: CheckCircle2 },
  failed: { label: "Failed", cls: styles.statusFail, Icon: XCircle },
  denied: { label: "Denied", cls: styles.statusDenied, Icon: ShieldOff },
} as const;

interface RunStatusBadgeProps {
  entry: Pick<SandboxExecutionLog, "succeeded" | "policyDenied">;
  size?: number;
}

const RunStatusBadge = ({ entry, size = 11 }: RunStatusBadgeProps) => {
  const { label, cls, Icon } = CONFIG[getRunStatus(entry)];
  return (
    <span className={`${styles.statusChip} ${cls}`}>
      <Icon size={size} />
      {label}
    </span>
  );
};

export default RunStatusBadge;
