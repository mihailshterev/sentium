import { Badge } from "@/shared/ui/badge";
import type { IncidentSeverity, IncidentStatus } from "@/features/vigil/types/serviceHealth";

interface Props {
  severity: IncidentSeverity;
  status: IncidentStatus;
}

export function SeverityBadge({ severity, status }: Props) {
  if (status === "Resolved") {
    return <Badge tone="neutral">Resolved</Badge>;
  }
  return <Badge tone={severity === "Critical" ? "danger" : "warning"}>{severity}</Badge>;
}
