import { Badge } from "@/shared/ui/badge";
import type { Role } from "../../../shared/utils/roles";

export function RoleBadge({ role }: { role: Role }) {
  return <Badge tone={role === "Sovereign" ? "accent" : "info"}>{role}</Badge>;
}
