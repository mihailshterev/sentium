import { queryOptions } from "@tanstack/react-query";
import { fetchVaultGraph, fetchVaultNote, fetchVaultNotes, fetchVaultSummary, fetchVaultTags } from "./vault.service";
import type { VaultNotesFilter } from "../types/vault";

export const vaultKeys = {
  all: ["vault"] as const,
  summary: () => [...vaultKeys.all, "summary"] as const,
  notes: (filter: VaultNotesFilter) => [...vaultKeys.all, "notes", filter] as const,
  note: (id: string) => [...vaultKeys.all, "note", id] as const,
  tags: () => [...vaultKeys.all, "tags"] as const,
  graph: (tag?: string) => [...vaultKeys.all, "graph", tag ?? ""] as const,
};

export const vaultQueries = {
  summary: () => queryOptions({ queryKey: vaultKeys.summary(), queryFn: () => fetchVaultSummary() }),
  notes: (filter: VaultNotesFilter) =>
    queryOptions({ queryKey: vaultKeys.notes(filter), queryFn: () => fetchVaultNotes(filter) }),
  note: (id: string) => queryOptions({ queryKey: vaultKeys.note(id), queryFn: () => fetchVaultNote(id) }),
  tags: () => queryOptions({ queryKey: vaultKeys.tags(), queryFn: () => fetchVaultTags() }),
  graph: (tag?: string) => queryOptions({ queryKey: vaultKeys.graph(tag), queryFn: () => fetchVaultGraph(tag) }),
};
