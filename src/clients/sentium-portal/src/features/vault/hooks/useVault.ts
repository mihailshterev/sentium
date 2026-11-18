import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  fetchVaultGraph,
  fetchVaultNote,
  fetchVaultNotes,
  fetchVaultSummary,
  fetchVaultTags,
  forgetVault,
} from "@/features/vault/api/vault.service";
import type { VaultNotesFilter } from "@/features/vault/types/vault";

export const vaultKeys = {
  all: ["vault"] as const,
  summary: ["vault", "summary"] as const,
  notes: (filter: VaultNotesFilter) => ["vault", "notes", filter] as const,
  note: (id: string) => ["vault", "note", id] as const,
  tags: ["vault", "tags"] as const,
  graph: (tag?: string) => ["vault", "graph", tag ?? ""] as const,
};

/** Notes keep arriving from Satelles while the page is open, so the summary refreshes on its own. */
const SUMMARY_POLL_INTERVAL = 30_000;

export function useVaultSummary() {
  return useQuery({
    queryKey: vaultKeys.summary,
    queryFn: fetchVaultSummary,
    refetchInterval: SUMMARY_POLL_INTERVAL,
  });
}

export function useVaultNotes(filter: VaultNotesFilter, enabled: boolean) {
  return useQuery({
    queryKey: vaultKeys.notes(filter),
    queryFn: () => fetchVaultNotes(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useVaultNote(id: string | null) {
  return useQuery({
    queryKey: vaultKeys.note(id ?? ""),
    queryFn: () => fetchVaultNote(id!),
    enabled: id !== null,
  });
}

export function useVaultTags(enabled: boolean) {
  return useQuery({ queryKey: vaultKeys.tags, queryFn: fetchVaultTags, enabled });
}

export function useVaultGraph(tag: string | undefined, enabled: boolean) {
  return useQuery({ queryKey: vaultKeys.graph(tag), queryFn: () => fetchVaultGraph(tag), enabled });
}

export function useForgetVault() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: forgetVault,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: vaultKeys.all }),
  });
}
