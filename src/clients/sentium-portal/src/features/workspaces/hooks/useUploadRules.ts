import { useQuery } from "@tanstack/react-query";
import { workspaceQueries } from "@/features/workspaces/api/workspaces.queries";

export function useUploadRules() {
  const { data } = useQuery(workspaceQueries.uploadRules());
  return data ?? null;
}
