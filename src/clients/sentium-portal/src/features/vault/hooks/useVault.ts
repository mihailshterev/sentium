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
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

export const vaultKeys = {
  all: ["vault"] as const,
  summary: ["vault", "summary"] as const,
  notes: (filter: VaultNotesFilter) => ["vault", "notes", filter] as const,
  note: (id: string) => ["vault", "note", id] as const,
  tags: ["vault", "tags"] as const,
  graph: (tag?: string) => ["vault", "graph", tag ?? ""] as const,
};

const VAULT_KEYS = [vaultKeys.all] as const;

/** Notes keep arriving from Satelles while the page is open; every sync step is reported as it lands. */
export function useVaultSummary() {
  useRealtimeInvalidate(REALTIME_TOPICS.vault, VAULT_KEYS);
  return useQuery({
    queryKey: vaultKeys.summary,
    queryFn: fetchVaultSummary,
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
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
