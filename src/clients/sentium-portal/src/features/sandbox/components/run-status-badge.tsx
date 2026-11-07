import { CheckCircle2, ShieldOff, XCircle } from "lucide-react";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";

type RunStatusKind = "succeeded" | "failed" | "denied";

function getRunStatus(entry: Pick<SandboxExecutionLog, "succeeded" | "policyDenied">): RunStatusKind {
  if (entry.policyDenied) {
    return "denied";
  }
  return entry.succeeded ? "succeeded" : "failed";
}

const CONFIG: Record<RunStatusKind, { label: string; tone: BadgeTone; Icon: typeof CheckCircle2 }> = {
  succeeded: { label: "Succeeded", tone: "success", Icon: CheckCircle2 },
  failed: { label: "Failed", tone: "danger", Icon: XCircle },
  denied: { label: "Denied", tone: "warning", Icon: ShieldOff },
};

interface RunStatusBadgeProps {
  entry: Pick<SandboxExecutionLog, "succeeded" | "policyDenied">;
  size?: number;
}

export function RunStatusBadge({ entry, size = 12 }: RunStatusBadgeProps) {
  const { label, tone, Icon } = CONFIG[getRunStatus(entry)];
  return (
    <Badge tone={tone}>
      <Icon size={size} aria-hidden="true" />
      {label}
    </Badge>
  );
}
