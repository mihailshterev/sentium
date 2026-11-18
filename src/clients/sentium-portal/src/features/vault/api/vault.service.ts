import { client } from "@/shared/api/client";
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

/** null until a vault has been synced (the API answers 204, which the client turns into an empty object). */
export const fetchVaultSummary = async (): Promise<VaultSummary | null> => {
  const summary = await client.get<Partial<VaultSummary>>(BASE);
  return summary.id ? (summary as VaultSummary) : null;
};

export const fetchVaultNotes = (filter: VaultNotesFilter): Promise<PagedResponse<VaultNoteSummary>> => {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== "") {
      params.set(key, String(value));
    }
  }

  const query = params.toString();
  return client.get<PagedResponse<VaultNoteSummary>>(`${BASE}/notes${query ? `?${query}` : ""}`);
};

export const fetchVaultNote = (id: string): Promise<VaultNote> =>
  client.get<VaultNote>(`${BASE}/notes/${encodeURIComponent(id)}`);

export const fetchVaultTags = (): Promise<VaultTagCount[]> => client.get<VaultTagCount[]>(`${BASE}/tags`);

export const fetchVaultGraph = (tag?: string): Promise<VaultGraph> =>
  client.get<VaultGraph>(`${BASE}/graph${tag ? `?tag=${encodeURIComponent(tag)}` : ""}`);

export const forgetVault = (): Promise<void> => client.delete<void>(BASE);
