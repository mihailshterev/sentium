import { useAuthStore } from "../stores/auth-store";
import type { Role } from "../utils/roles";
import { ROLE_HIERARCHY } from "../utils/roles";

export function useRole() {
  const user = useAuthStore((s) => s.user);
  const roles = user?.roles ?? [];

  const highestRole = [...roles]
    .filter((r): r is Role => ROLE_HIERARCHY.includes(r as Role))
    .sort((a, b) => ROLE_HIERARCHY.indexOf(b as Role) - ROLE_HIERARCHY.indexOf(a as Role))[0] as Role | undefined;

  const isSovereign = roles.includes("Sovereign");
  const isMemberOrAbove = isSovereign || roles.includes("Member");
  const isAuthenticated = roles.length > 0;

  function hasRole(role: Role): boolean {
    return roles.includes(role);
  }

  return { roles, highestRole, isSovereign, isMemberOrAbove, isAuthenticated, hasRole };
}
