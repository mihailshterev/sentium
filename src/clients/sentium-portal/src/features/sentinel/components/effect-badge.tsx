import { CheckCircle, ShieldOff, Siren } from "lucide-react";
import { Badge } from "@/shared/ui/badge";

export function EffectBadge({ allowed, effect }: { allowed: boolean; effect: string }) {
  if (allowed) {
    return (
      <Badge tone="success">
        <CheckCircle size={12} aria-hidden="true" /> Allow
      </Badge>
    );
  }
  if (effect === "DenyWithAlert") {
    return (
      <Badge tone="warning">
        <Siren size={12} aria-hidden="true" /> Alert
      </Badge>
    );
  }
  return (
    <Badge tone="danger">
      <ShieldOff size={12} aria-hidden="true" /> Deny
    </Badge>
  );
}
