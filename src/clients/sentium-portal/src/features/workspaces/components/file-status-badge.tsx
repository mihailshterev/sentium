import { Badge, type BadgeTone } from "@/shared/ui/badge";
import type { WorkspaceFile } from "@/features/workspaces/types/workspace";

const TONE: Record<WorkspaceFile["processingStatus"], BadgeTone> = {
  Pending: "neutral",
  Processing: "info",
  Completed: "success",
  Failed: "danger",
};

export function FileStatusBadge({ status }: { status: WorkspaceFile["processingStatus"] }) {
  return <Badge tone={TONE[status] ?? "neutral"}>{status}</Badge>;
}
