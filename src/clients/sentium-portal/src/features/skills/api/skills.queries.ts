import { queryOptions } from "@tanstack/react-query";
import { fetchBuiltInSkills } from "./skills.service";
import type { AgentSkillType } from "../types/skills";

export const skillKeys = {
  all: ["skills"] as const,
  list: (skillType?: AgentSkillType) => [...skillKeys.all, "list", skillType ?? "all"] as const,
  builtIn: () => [...skillKeys.all, "built-in"] as const,
};

export const skillQueries = {
  builtIn: () =>
    queryOptions({ queryKey: skillKeys.builtIn(), queryFn: () => fetchBuiltInSkills(), staleTime: Infinity }),
};
