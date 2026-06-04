export const ROLES = {
  Sovereign: "Sovereign",
  Member: "Member",
} as const;

export type Role = (typeof ROLES)[keyof typeof ROLES];

export const ROLE_HIERARCHY: Role[] = ["Member", "Sovereign"];

export function outranks(actorRole: Role | string, targetRole: Role | string): boolean {
  return ROLE_HIERARCHY.indexOf(actorRole as Role) > ROLE_HIERARCHY.indexOf(targetRole as Role);
}

export const ROLE_LABELS: Record<Role, string> = {
  Sovereign: "Sovereign",
  Member: "Member",
};

export const ROLE_BADGE_VARIANT: Record<Role, "sovereign" | "member"> = {
  Sovereign: "sovereign",
  Member: "member",
};
