import { useQuery } from "@tanstack/react-query";
import { fetchUploadRules } from "@/features/workspaces/api/workspaces.service";

/** The server's upload rules; they only change with a deployment, so they are fetched once per session. */
export function useUploadRules() {
  const { data } = useQuery({
    queryKey: ["workspaceUploadRules"],
    queryFn: fetchUploadRules,
    staleTime: Infinity,
    gcTime: Infinity,
  });
  return data ?? null;
}
