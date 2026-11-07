import { useCallback, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { FolderTree, MapPin, Package, Plus } from "lucide-react";
import styles from "../inventory.module.css";
import { ApiError } from "@/shared/api/client";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { AssetFilterBar } from "../components/asset-filter-bar";
import { AssetFormDialog } from "../components/asset-form-dialog";
import { AssetList } from "../components/asset-list";
import { DeleteLocationDialog } from "../components/delete-location-dialog";
import { LocationFormDialog } from "../components/location-form-dialog";
import { LocationTree } from "../components/location-tree";
import {
  useAssetMutations,
  useAssetSearch,
  useLocationMutations,
  useLocations,
  useTaxonomy,
} from "../hooks/useInventory";
import type { AssetFilters, AssetSort, InventoryLocation } from "../types/inventory";

type LocationDialog = { mode: "create"; parentId: string | null } | { mode: "edit"; location: InventoryLocation };

const errorMessage = (error: unknown) =>
  error instanceof ApiError || error instanceof Error ? error.message : error ? "Something went wrong." : null;

function useFilterParams(): [AssetFilters, (changes: Partial<AssetFilters>) => void] {
  const [params, setParams] = useSearchParams();

  const filters = useMemo<AssetFilters>(() => {
    const shared = params.get("shared");
    return {
      q: params.get("q") ?? "",
      locationId: params.get("location"),
      includeSubLocations: params.get("nested") !== "false",
      category: params.get("category"),
      tags: params.getAll("tag"),
      agentAccessible: shared === null ? null : shared === "true",
      sort: (params.get("sort") as AssetSort | null) ?? "Name",
    };
  }, [params]);

  const update = useCallback(
    (changes: Partial<AssetFilters>) =>
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          const set = (key: string, value: string | null | undefined) =>
            value ? next.set(key, value) : next.delete(key);

          if ("q" in changes) set("q", changes.q?.trim());
          if ("locationId" in changes) set("location", changes.locationId);
          if ("includeSubLocations" in changes) set("nested", changes.includeSubLocations === false ? "false" : null);
          if ("category" in changes) set("category", changes.category);
          if ("agentAccessible" in changes)
            set("shared", changes.agentAccessible == null ? null : String(changes.agentAccessible));
          if ("sort" in changes) set("sort", changes.sort === "Name" ? null : changes.sort);
          if ("tags" in changes) {
            next.delete("tag");
            for (const tag of changes.tags ?? []) next.append("tag", tag);
          }
          return next;
        },
        { replace: true },
      ),
    [setParams],
  );

  return [filters, update];
}

export function InventoryView() {
  const navigate = useNavigate();
  const [filters, updateFilters] = useFilterParams();

  const { locations, isLoading: locationsLoading, isError: locationsError } = useLocations();
  const { tags, categories } = useTaxonomy();
  const search = useAssetSearch(filters);
  const locationMutations = useLocationMutations();
  const assetMutations = useAssetMutations();

  const [locationDialog, setLocationDialog] = useState<LocationDialog | null>(null);
  const [locationToDelete, setLocationToDelete] = useState<InventoryLocation | null>(null);
  const [assetDialogOpen, setAssetDialogOpen] = useState(false);

  const selected = locations.find((l) => l.id === filters.locationId) ?? null;
  const hasFilters = !!(filters.q || filters.category || filters.tags?.length || filters.agentAccessible != null);

  const closeLocationDialog = () => {
    setLocationDialog(null);
    locationMutations.create.reset();
    locationMutations.update.reset();
  };

  const openNewAsset = () => {
    assetMutations.create.reset();
    setAssetDialogOpen(true);
  };

  return (
    <Page
      title="Inventory"
      subtitle="Where everything in the household lives — and what assistants may know about it"
      icon={<Package />}
      width="wide"
      actions={
        <>
          <Button
            variant="secondary"
            icon={<MapPin size={15} />}
            onClick={() => setLocationDialog({ mode: "create", parentId: filters.locationId ?? null })}
          >
            New location
          </Button>
          <Button variant="primary" icon={<Plus size={15} />} onClick={openNewAsset} disabled={locations.length === 0}>
            New asset
          </Button>
        </>
      }
    >
      <div className={styles.layout}>
        <Card as="section" padding="none" className={styles.treeCard} aria-label="Locations">
          <CardHeader icon={<FolderTree />} title="Locations" />
          {locationsLoading && (
            <div className={styles.listPadding}>
              <SkeletonText lines={4} />
            </div>
          )}
          {locationsError && (
            <div className={styles.listPadding}>
              <StatusMessage variant="error" message="Failed to load locations." />
            </div>
          )}
          {!locationsLoading && !locationsError && locations.length === 0 && (
            <EmptyState
              compact
              icon={<MapPin />}
              title="No locations yet"
              hint="Start with a place such as your home, then add rooms inside it."
              action={
                <Button
                  size="sm"
                  variant="primary"
                  onClick={() => setLocationDialog({ mode: "create", parentId: null })}
                >
                  Add a location
                </Button>
              }
            />
          )}
          {locations.length > 0 && (
            <LocationTree
              locations={locations}
              selectedId={filters.locationId ?? null}
              onSelect={(id) => updateFilters({ locationId: id })}
              onAddChild={(parent) => setLocationDialog({ mode: "create", parentId: parent.id })}
              onEdit={(location) => setLocationDialog({ mode: "edit", location })}
              onDelete={(location) => {
                locationMutations.remove.reset();
                setLocationToDelete(location);
              }}
            />
          )}
        </Card>

        <section className={styles.main} aria-label="Assets">
          <AssetFilterBar filters={filters} onChange={updateFilters} categories={categories} tags={tags} />

          <Card padding="none" as="section">
            <CardHeader
              icon={<Package />}
              title={selected ? selected.path : "All assets"}
              description={selected?.description ?? undefined}
              actions={
                <Badge tone="neutral">
                  {search.totalCount} item{search.totalCount === 1 ? "" : "s"}
                </Badge>
              }
            />
            <AssetList
              assets={search.assets}
              isLoading={search.isLoading}
              isError={search.isError}
              hasMore={search.hasMore}
              isLoadingMore={search.isLoadingMore}
              onLoadMore={() => search.loadMore()}
              hasFilters={hasFilters}
              emptyAction={
                locations.length > 0 ? (
                  <Button size="sm" variant="primary" icon={<Plus size={14} />} onClick={openNewAsset}>
                    New asset
                  </Button>
                ) : undefined
              }
            />
          </Card>
        </section>
      </div>

      {locationDialog && (
        <LocationFormDialog
          locations={locations}
          location={locationDialog.mode === "edit" ? locationDialog.location : null}
          parentId={locationDialog.mode === "create" ? locationDialog.parentId : null}
          isPending={locationMutations.create.isPending || locationMutations.update.isPending}
          error={errorMessage(locationMutations.create.error ?? locationMutations.update.error)}
          onCancel={closeLocationDialog}
          onSubmit={(payload) => {
            if (locationDialog.mode === "edit") {
              locationMutations.update.mutate(
                { id: locationDialog.location.id, payload },
                { onSuccess: closeLocationDialog },
              );
            } else {
              locationMutations.create.mutate(payload, {
                onSuccess: (created) => {
                  closeLocationDialog();
                  updateFilters({ locationId: created.id });
                },
              });
            }
          }}
        />
      )}

      {locationToDelete && (
        <DeleteLocationDialog
          location={locationToDelete}
          locations={locations}
          isPending={locationMutations.remove.isPending}
          error={errorMessage(locationMutations.remove.error)}
          onCancel={() => setLocationToDelete(null)}
          onConfirm={(moveContentsTo) =>
            locationMutations.remove.mutate(
              { id: locationToDelete.id, moveContentsTo },
              {
                onSuccess: () => {
                  if (filters.locationId === locationToDelete.id) updateFilters({ locationId: moveContentsTo });
                  setLocationToDelete(null);
                },
              },
            )
          }
        />
      )}

      {assetDialogOpen && (
        <AssetFormDialog
          locations={locations}
          defaultLocationId={filters.locationId ?? (locations.length === 1 ? locations[0].id : null)}
          categories={categories}
          tagSuggestions={tags.map((t) => t.name)}
          isPending={assetMutations.create.isPending}
          error={errorMessage(assetMutations.create.error)}
          onCancel={() => setAssetDialogOpen(false)}
          onSubmit={(payload) =>
            assetMutations.create.mutate(payload, {
              onSuccess: (created) => {
                setAssetDialogOpen(false);
                navigate(`/inventory/assets/${created.id}`);
              },
            })
          }
        />
      )}
    </Page>
  );
}
