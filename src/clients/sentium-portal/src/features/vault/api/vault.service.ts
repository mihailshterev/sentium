import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type {
  PagedResponse,
  VaultGraph,
  VaultNote,
  VaultNoteSummary,
  VaultNotesFilter,
  VaultSummary,
  VaultTagCount,
} from "@/features/vault/types/vault";

const BASE = "/memoria/vault";
const url = pathIn(BASE);

/** null until a vault has been synced (the API answers 204). */
export const fetchVaultSummary = async (): Promise<VaultSummary | null> =>
  (await client.get<VaultSummary | undefined>(BASE)) ?? null;

export const fetchVaultNotes = (filter: VaultNotesFilter): Promise<PagedResponse<VaultNoteSummary>> =>
  client.get<PagedResponse<VaultNoteSummary>>(withQuery(`${BASE}/notes`, filter));

export const fetchVaultNote = (id: string): Promise<VaultNote> =>
  client.get<VaultNote>(url`/notes/${id}`);

export const fetchVaultTags = (): Promise<VaultTagCount[]> => client.get<VaultTagCount[]>(`${BASE}/tags`);

export const fetchVaultGraph = (tag?: string): Promise<VaultGraph> =>
  client.get<VaultGraph>(withQuery(`${BASE}/graph`, { tag }));

export const forgetVault = (): Promise<void> => client.delete<void>(BASE);
