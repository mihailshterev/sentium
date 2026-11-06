import { Badge, type BadgeTone } from "@/shared/ui/badge";
import type { PolicyRiskLevel } from "@/features/sentinel/types/sentinel";

const TONE: Record<PolicyRiskLevel, BadgeTone> = {
  Low: "success",
  Medium: "accent",
  High: "warning",
  Critical: "danger",
};

export function RiskBadge({ risk }: { risk: PolicyRiskLevel }) {
  return <Badge tone={TONE[risk] ?? "neutral"}>{risk}</Badge>;
}
