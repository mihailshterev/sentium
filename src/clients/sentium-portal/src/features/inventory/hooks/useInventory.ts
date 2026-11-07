import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useInfiniteList } from "@/shared/hooks/useInfiniteList";
import * as api from "../api/inventory.service";
import type { AssetFilters, AttachmentKind, SaveAssetPayload, SaveLocationPayload } from "../types/inventory";

export const inventoryKeys = {
  all: ["inventory"] as const,
  locations: () => ["inventory", "locations"] as const,
  assets: (filters: AssetFilters) => ["inventory", "assets", filters] as const,
  asset: (id: string) => ["inventory", "asset", id] as const,
  tags: () => ["inventory", "tags"] as const,
  categories: () => ["inventory", "categories"] as const,
};

const ASSET_PAGE_SIZE = 30;
const PENDING_POLL_MS = 4000;

function useInvalidateInventory() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: inventoryKeys.all });
}

export function useLocations() {
  const query = useQuery({ queryKey: inventoryKeys.locations(), queryFn: api.fetchLocations });
  return {
    locations: query.data ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
  };
}

export function useLocationMutations() {
  const invalidate = useInvalidateInventory();

  const create = useMutation({
    mutationFn: (payload: SaveLocationPayload) => api.createLocation(payload),
    onSuccess: invalidate,
  });
  const update = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SaveLocationPayload }) => api.updateLocation(id, payload),
    onSuccess: invalidate,
  });
  const remove = useMutation({
    mutationFn: ({ id, moveContentsTo }: { id: string; moveContentsTo?: string | null }) =>
      api.deleteLocation(id, moveContentsTo),
    onSuccess: invalidate,
  });

  return { create, update, remove };
}

export function useAssetSearch(filters: AssetFilters) {
  const list = useInfiniteList(
    inventoryKeys.assets(filters),
    (page, pageSize) => api.searchAssets(filters, page, pageSize),
    { pageSize: ASSET_PAGE_SIZE },
  );
  return {
    assets: list.items,
    totalCount: list.totalCount,
    isLoading: list.isLoading,
    isError: !!list.error,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isLoadingMore: list.isLoadingMore,
  };
}

export function useAsset(id: string | undefined) {
  const query = useQuery({
    queryKey: inventoryKeys.asset(id ?? ""),
    queryFn: () => api.fetchAsset(id!),
    enabled: !!id,
    refetchInterval: (q) => (q.state.data?.knowledgeStatus === "Pending" ? PENDING_POLL_MS : false),
  });
  return { asset: query.data ?? null, isLoading: query.isLoading, error: query.error };
}

export function useAssetMutations() {
  const invalidate = useInvalidateInventory();

  const create = useMutation({
    mutationFn: (payload: SaveAssetPayload) => api.createAsset(payload),
    onSuccess: invalidate,
  });
  const update = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SaveAssetPayload }) => api.updateAsset(id, payload),
    onSuccess: invalidate,
  });
  const remove = useMutation({ mutationFn: (id: string) => api.deleteAsset(id), onSuccess: invalidate });
  const retrySync = useMutation({ mutationFn: (id: string) => api.requestKnowledgeSync(id), onSuccess: invalidate });

  return { create, update, remove, retrySync };
}

export function useAttachmentMutations(assetId: string) {
  const invalidate = useInvalidateInventory();

  const upload = useMutation({
    mutationFn: ({ file, kind }: { file: File; kind: AttachmentKind }) => api.uploadAttachment(assetId, file, kind),
    onSuccess: invalidate,
  });
  const remove = useMutation({
    mutationFn: (attachmentId: string) => api.deleteAttachment(assetId, attachmentId),
    onSuccess: invalidate,
  });

  return { upload, remove };
}

export function useTaxonomy() {
  const tags = useQuery({ queryKey: inventoryKeys.tags(), queryFn: () => api.fetchTags() });
  const categories = useQuery({ queryKey: inventoryKeys.categories(), queryFn: api.fetchCategories });
  return {
    tags: tags.data ?? [],
    categories: categories.data ?? [],
  };
}
