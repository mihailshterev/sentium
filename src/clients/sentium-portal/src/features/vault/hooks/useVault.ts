import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { forgetVault } from "@/features/vault/api/vault.service";
import { vaultKeys, vaultQueries } from "@/features/vault/api/vault.queries";
import type { VaultNotesFilter } from "@/features/vault/types/vault";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const VAULT_KEYS = [vaultKeys.all] as const;

/** Notes keep arriving from Satelles while the page is open; every sync step is reported as it lands. */
export function useVaultSummary() {
  useRealtimeInvalidate(REALTIME_TOPICS.vault, VAULT_KEYS);
  return useQuery({
    ...vaultQueries.summary(),
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
  });
}

export function useVaultNotes(filter: VaultNotesFilter, enabled: boolean) {
  return useQuery({
    ...vaultQueries.notes(filter),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useVaultNote(id: string | null) {
  return useQuery({
    ...vaultQueries.note(id ?? ""),
    enabled: id !== null,
  });
}

export function useVaultTags(enabled: boolean) {
  return useQuery({ ...vaultQueries.tags(), enabled });
}

export function useVaultGraph(tag: string | undefined, enabled: boolean) {
  return useQuery({ ...vaultQueries.graph(tag), enabled });
}

export function useForgetVault() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: forgetVault,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: vaultKeys.all }),
  });
}
