import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { PagedResponse } from "@/shared/types/pagination";
import type {
  Asset,
  AssetFilters,
  AssetSummary,
  Attachment,
  AttachmentKind,
  InventoryLocation,
  SaveAssetPayload,
  SaveLocationPayload,
  TagCount,
} from "../types/inventory";

const BASE = "/locus";
const url = pathIn(BASE);

export const fetchLocations = (): Promise<InventoryLocation[]> => client.get<InventoryLocation[]>(`${BASE}/locations`);

export const createLocation = (payload: SaveLocationPayload): Promise<InventoryLocation> =>
  client.post<InventoryLocation>(`${BASE}/locations`, payload);

export const updateLocation = (id: string, payload: SaveLocationPayload): Promise<InventoryLocation> =>
  client.put<InventoryLocation>(url`/locations/${id}`, payload);

export const deleteLocation = (id: string, moveContentsTo?: string | null): Promise<void> =>
  client.delete<void>(withQuery(url`/locations/${id}`, { moveContentsTo }));

export function buildAssetQuery(filters: AssetFilters, page: number, pageSize: number): string {
  const params = new URLSearchParams();
  const q = filters.q?.trim();
  if (q) params.set("q", q);
  if (filters.locationId) {
    params.set("locationId", filters.locationId);
    if (filters.includeSubLocations === false) params.set("includeSubLocations", "false");
  }
  if (filters.category) params.set("category", filters.category);
  for (const tag of filters.tags ?? []) params.append("tag", tag);
  if (filters.agentAccessible != null) params.set("agentAccessible", String(filters.agentAccessible));
  if (filters.sort && filters.sort !== "Name") params.set("sort", filters.sort);
  params.set("page", String(page));
  params.set("pageSize", String(pageSize));
  return params.toString();
}

export const searchAssets = (filters: AssetFilters, page = 1, pageSize = 30): Promise<PagedResponse<AssetSummary>> =>
  client.get<PagedResponse<AssetSummary>>(`${BASE}/assets?${buildAssetQuery(filters, page, pageSize)}`);

export const fetchAsset = (id: string): Promise<Asset> => client.get<Asset>(url`/assets/${id}`);

export const createAsset = (payload: SaveAssetPayload): Promise<Asset> => client.post<Asset>(`${BASE}/assets`, payload);

export const updateAsset = (id: string, payload: SaveAssetPayload): Promise<Asset> =>
  client.put<Asset>(url`/assets/${id}`, payload);

export const deleteAsset = (id: string): Promise<void> => client.delete<void>(url`/assets/${id}`);

export const requestKnowledgeSync = (id: string): Promise<void> =>
  client.post<void>(url`/assets/${id}/knowledge-sync`, undefined);

export const fetchTags = (prefix?: string): Promise<TagCount[]> =>
  client.get<TagCount[]>(withQuery(`${BASE}/tags`, { prefix }));

export const fetchCategories = (): Promise<string[]> => client.get<string[]>(`${BASE}/categories`);

export const uploadAttachment = (assetId: string, file: File, kind: AttachmentKind): Promise<Attachment> => {
  const form = new FormData();
  form.append("file", file);
  form.append("kind", kind);
  return client.postForm<Attachment>(url`/assets/${assetId}/attachments`, form);
};

export const deleteAttachment = (assetId: string, attachmentId: string): Promise<void> =>
  client.delete<void>(url`/assets/${assetId}/attachments/${attachmentId}`);

export const downloadUrl = (attachment: Attachment): string => `${attachment.contentUrl}?download=true`;
