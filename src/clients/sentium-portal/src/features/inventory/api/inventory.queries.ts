import { queryOptions } from "@tanstack/react-query";
import { fetchAsset, fetchCategories, fetchLocations, fetchTags } from "./inventory.service";
import type { AssetFilters } from "../types/inventory";

export const inventoryKeys = {
  all: ["inventory"] as const,
  locations: () => [...inventoryKeys.all, "locations"] as const,
  assets: (filters: AssetFilters) => [...inventoryKeys.all, "assets", filters] as const,
  asset: (id: string) => [...inventoryKeys.all, "asset", id] as const,
  tags: () => [...inventoryKeys.all, "tags"] as const,
  categories: () => [...inventoryKeys.all, "categories"] as const,
};

export const inventoryQueries = {
  locations: () => queryOptions({ queryKey: inventoryKeys.locations(), queryFn: () => fetchLocations() }),
  asset: (id: string) => queryOptions({ queryKey: inventoryKeys.asset(id), queryFn: () => fetchAsset(id) }),
  tags: () => queryOptions({ queryKey: inventoryKeys.tags(), queryFn: () => fetchTags() }),
  categories: () => queryOptions({ queryKey: inventoryKeys.categories(), queryFn: () => fetchCategories() }),
};
