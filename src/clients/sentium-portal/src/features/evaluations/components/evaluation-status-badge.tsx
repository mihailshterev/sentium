import { Badge, type BadgeTone } from "@/shared/ui/badge";
import type { EvaluationStatus } from "../types/evaluations";

const TONES: Record<EvaluationStatus, BadgeTone> = {
  Queued: "neutral",
  Running: "info",
  Completed: "success",
  Failed: "danger",
  Cancelled: "warning",
};

export function EvaluationStatusBadge({ status }: { status: EvaluationStatus }) {
  return (
    <Badge tone={TONES[status]} dot={status === "Running"}>
      {status}
    </Badge>
  );
}
