import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Box,
  Building2,
  ChevronDown,
  ChevronRight,
  EyeOff,
  Loader2,
  MapPin,
  Package,
  Pencil,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import styles from "./inventory.module.scss";
import {
  fetchLocations,
  createLocation,
  updateLocation,
  deleteLocation,
  fetchAssetsByLocation,
  createAsset,
  updateAsset,
  deleteAsset,
} from "../../services/locus.service";
import type { Location, Asset, CreateLocationPayload, CreateAssetPayload } from "../../types/locus";

function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  return new Date(iso).toLocaleDateString(undefined, { dateStyle: "medium" });
}

interface LocationFormProps {
  initial?: { name: string; description: string; accessNotes: string };
  parentId?: string | null;
  title: string;
  isPending: boolean;
  onSubmit: (payload: CreateLocationPayload) => void;
  onCancel: () => void;
}

const LocationForm = ({ initial, parentId, title, isPending, onSubmit, onCancel }: LocationFormProps) => {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [accessNotes, setAccessNotes] = useState(initial?.accessNotes ?? "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (name.trim()) {
      onSubmit({
        name: name.trim(),
        description: description.trim() || undefined,
        accessNotes: accessNotes.trim() || undefined,
        parentLocationId: parentId ?? null,
      });
    }
  };

  return (
    <div className={styles.modalOverlay} onClick={onCancel}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <span className={styles.modalTitle}>{title}</span>
          <button className={styles.modalClose} onClick={onCancel}>
            <X size={14} />
          </button>
        </div>
        <form onSubmit={handleSubmit} className={styles.modalForm}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="loc-name">
              Name
            </label>
            <input
              id="loc-name"
              className={styles.input}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Kitchen, Basement Storage"
              autoFocus
              required
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="loc-desc">
              Description
            </label>
            <textarea
              id="loc-desc"
              className={`${styles.input} ${styles.textarea}`}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description…"
              rows={2}
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="loc-access">
              Access Notes
            </label>
            <textarea
              id="loc-access"
              className={`${styles.input} ${styles.textarea}`}
              value={accessNotes}
              onChange={(e) => setAccessNotes(e.target.value)}
              placeholder="e.g. Key is in the kitchen drawer"
              rows={2}
            />
          </div>
        </form>
        <div className={styles.modalActions}>
          <button type="button" className={styles.cancelButton} onClick={onCancel}>
            Cancel
          </button>
          <button
            type="submit"
            form="loc-form"
            className={styles.submitButton}
            disabled={!name.trim() || isPending}
            onClick={(e) => {
              e.preventDefault();
              if (name.trim())
                onSubmit({
                  name: name.trim(),
                  description: description.trim() || undefined,
                  accessNotes: accessNotes.trim() || undefined,
                  parentLocationId: parentId ?? null,
                });
            }}
          >
            {isPending && <Loader2 size={13} className={styles.spin} />}
            {title}
          </button>
        </div>
      </div>
    </div>
  );
};

interface AssetFormProps {
  initial?: Asset;
  locationId: string;
  title: string;
  isPending: boolean;
  onSubmit: (payload: CreateAssetPayload) => void;
  onCancel: () => void;
}

const AssetForm = ({ initial, locationId, title, isPending, onSubmit, onCancel }: AssetFormProps) => {
  const [displayName, setDisplayName] = useState(initial?.displayName ?? "");
  const [category, setCategory] = useState(initial?.category ?? "");
  const [physicalDescription, setPhysicalDescription] = useState(initial?.physicalDescription ?? "");
  const [instructions, setInstructions] = useState(initial?.instructions ?? "");
  const [manufacturer, setManufacturer] = useState(initial?.manufacturer ?? "");
  const [modelNumber, setModelNumber] = useState(initial?.modelNumber ?? "");
  const [serialNumber, setSerialNumber] = useState(initial?.serialNumber ?? "");
  const [purchaseDate, setPurchaseDate] = useState(initial?.purchaseDate ? initial.purchaseDate.substring(0, 10) : "");
  const [lastServicedDate, setLastServicedDate] = useState(
    initial?.lastServicedDate ? initial.lastServicedDate.substring(0, 10) : "",
  );
  const [warrantyInfo, setWarrantyInfo] = useState(initial?.warrantyInfo ?? "");
  const [isAgentAccessible, setIsAgentAccessible] = useState(initial?.isAgentAccessible ?? true);
  const [agentInstructions, setAgentInstructions] = useState(initial?.agentInstructions ?? "");

  const handleSubmit = () => {
    if (!displayName.trim()) {
      return;
    }
    onSubmit({
      displayName: displayName.trim(),
      category: category.trim() || undefined,
      physicalDescription: physicalDescription.trim() || undefined,
      instructions: instructions.trim() || undefined,
      manufacturer: manufacturer.trim() || undefined,
      modelNumber: modelNumber.trim() || undefined,
      serialNumber: serialNumber.trim() || undefined,
      purchaseDate: purchaseDate || null,
      lastServicedDate: lastServicedDate || null,
      warrantyInfo: warrantyInfo.trim() || undefined,
      isAgentAccessible,
      agentInstructions: agentInstructions.trim() || undefined,
      locationId,
    });
  };

  return (
    <div className={styles.modalOverlay} onClick={onCancel}>
      <div className={styles.modalLarge} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <span className={styles.modalTitle}>{title}</span>
          <button className={styles.modalClose} onClick={onCancel}>
            <X size={14} />
          </button>
        </div>
        <div className={styles.modalForm}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="asset-name">
              Display Name *
            </label>
            <input
              id="asset-name"
              className={styles.input}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="e.g. Main Wi-Fi Router, Fire Extinguisher"
              autoFocus
              required
            />
          </div>
          <div className={styles.formRow}>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-cat">
                Category
              </label>
              <input
                id="asset-cat"
                className={styles.input}
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                placeholder="Appliances, Documents…"
              />
            </div>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-mfr">
                Manufacturer
              </label>
              <input
                id="asset-mfr"
                className={styles.input}
                value={manufacturer}
                onChange={(e) => setManufacturer(e.target.value)}
                placeholder="e.g. TP-Link"
              />
            </div>
          </div>
          <div className={styles.formRow}>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-model">
                Model
              </label>
              <input
                id="asset-model"
                className={styles.input}
                value={modelNumber}
                onChange={(e) => setModelNumber(e.target.value)}
                placeholder="Model number"
              />
            </div>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-serial">
                Serial Number
              </label>
              <input
                id="asset-serial"
                className={styles.input}
                value={serialNumber}
                onChange={(e) => setSerialNumber(e.target.value)}
                placeholder="Serial / barcode"
              />
            </div>
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="asset-loc">
              Physical Description
            </label>
            <input
              id="asset-loc"
              className={styles.input}
              value={physicalDescription}
              onChange={(e) => setPhysicalDescription(e.target.value)}
              placeholder="e.g. Under the sink, left side"
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="asset-notes">
              Manual Notes / Instructions
            </label>
            <textarea
              id="asset-notes"
              className={`${styles.input} ${styles.textarea}`}
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
              placeholder="Maintenance history, access codes, manuals…"
              rows={3}
            />
          </div>

          <div className={styles.sectionDivider}>Lifecycle</div>

          <div className={styles.formRow}>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-purchase">
                Purchase Date
              </label>
              <input
                id="asset-purchase"
                type="date"
                className={styles.input}
                value={purchaseDate}
                onChange={(e) => setPurchaseDate(e.target.value)}
              />
            </div>
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-serviced">
                Last Serviced
              </label>
              <input
                id="asset-serviced"
                type="date"
                className={styles.input}
                value={lastServicedDate}
                onChange={(e) => setLastServicedDate(e.target.value)}
              />
            </div>
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="asset-warranty">
              Warranty Info
            </label>
            <input
              id="asset-warranty"
              className={styles.input}
              value={warrantyInfo}
              onChange={(e) => setWarrantyInfo(e.target.value)}
              placeholder="Expiry, provider, claim reference…"
            />
          </div>

          <div className={styles.sectionDivider}>Agent Access</div>

          <label className={styles.checkboxRow}>
            <input
              type="checkbox"
              checked={isAgentAccessible}
              onChange={(e) => setIsAgentAccessible(e.target.checked)}
            />
            <span className={styles.checkboxLabel}>Allow agents to access and reason about this asset</span>
          </label>
          {isAgentAccessible && (
            <div className={styles.formGroup}>
              <label className={styles.label} htmlFor="asset-agent-instr">
                Agent Instructions
              </label>
              <textarea
                id="asset-agent-instr"
                className={`${styles.input} ${styles.textarea}`}
                value={agentInstructions}
                onChange={(e) => setAgentInstructions(e.target.value)}
                placeholder='e.g. "Never suggest moving this", "Check warranty before advising repair"'
                rows={2}
              />
            </div>
          )}
        </div>
        <div className={styles.modalActions}>
          <button type="button" className={styles.cancelButton} onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            className={styles.submitButton}
            disabled={!displayName.trim() || isPending}
            onClick={handleSubmit}
          >
            {isPending && <Loader2 size={13} className={styles.spin} />}
            {title}
          </button>
        </div>
      </div>
    </div>
  );
};

interface LocationTreeItemProps {
  loc: Location;
  allLocations: Location[];
  selectedId: string | null;
  onSelect: (loc: Location) => void;
  onEdit: (loc: Location) => void;
  onDelete: (loc: Location) => void;
  onAddChild: (parentId: string) => void;
  depth?: number;
}

const LocationTreeItem = ({
  loc,
  allLocations,
  selectedId,
  onSelect,
  onEdit,
  onDelete,
  onAddChild,
  depth = 0,
}: LocationTreeItemProps) => {
  const [expanded, setExpanded] = useState(true);
  const children = allLocations.filter((l) => l.parentLocationId === loc.id);
  const hasChildren = children.length > 0 || loc.subLocationCount > 0;

  return (
    <div>
      <div
        className={`${styles.locationItem} ${selectedId === loc.id ? styles.locationItemActive : ""}`}
        onClick={() => onSelect(loc)}
      >
        <div className={styles.locationRow}>
          {hasChildren ? (
            <button
              className={styles.locExpandBtn}
              onClick={(e) => {
                e.stopPropagation();
                setExpanded((x) => !x);
              }}
            >
              {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
            </button>
          ) : (
            <span className={styles.locExpandSpacer} />
          )}
          <Building2 size={14} className={styles.locIcon} />
          <div className={styles.locInfo}>
            <span className={styles.locName}>{loc.name}</span>
            <div className={styles.locMeta}>
              <span className={styles.locCount}>
                {loc.assetCount} asset{loc.assetCount !== 1 ? "s" : ""}
              </span>
            </div>
          </div>
          <div className={styles.locActions} onClick={(e) => e.stopPropagation()}>
            <button className={styles.iconBtn} title="Add sub-location" onClick={() => onAddChild(loc.id)}>
              <Plus size={12} />
            </button>
            <button className={styles.iconBtn} title="Edit" onClick={() => onEdit(loc)}>
              <Pencil size={12} />
            </button>
            <button className={styles.iconBtnDanger} title="Delete" onClick={() => onDelete(loc)}>
              <Trash2 size={12} />
            </button>
          </div>
        </div>
      </div>

      {expanded && children.length > 0 && (
        <div className={styles.subLocationList}>
          {children.map((child) => (
            <LocationTreeItem
              key={child.id}
              loc={child}
              allLocations={allLocations}
              selectedId={selectedId}
              onSelect={onSelect}
              onEdit={onEdit}
              onDelete={onDelete}
              onAddChild={onAddChild}
              depth={depth + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
};

interface AssetCardProps {
  asset: Asset;
  onEdit: (asset: Asset) => void;
  onDelete: (asset: Asset) => void;
}

const AssetCard = ({ asset, onEdit, onDelete }: AssetCardProps) => (
  <div className={styles.assetCard}>
    <div className={styles.assetIconWrap}>
      <Box size={16} />
    </div>
    <div className={styles.assetBody}>
      <div className={styles.assetName}>{asset.displayName}</div>
      <div className={styles.assetMeta}>
        {asset.category && <span className={styles.assetTagCyan}>{asset.category}</span>}
        {asset.manufacturer && (
          <span className={styles.assetTag}>
            {asset.manufacturer}
            {asset.modelNumber ? ` · ${asset.modelNumber}` : ""}
          </span>
        )}
        {asset.lastServicedDate && (
          <span className={styles.assetTag}>Serviced {formatDate(asset.lastServicedDate)}</span>
        )}
        {asset.purchaseDate && <span className={styles.assetTag}>Purchased {formatDate(asset.purchaseDate)}</span>}
        {!asset.isAgentAccessible && (
          <span className={styles.agentHidden}>
            <EyeOff size={10} /> Hidden from agents
          </span>
        )}
      </div>
      {asset.physicalDescription && (
        <div className={styles.assetDesc}>
          <MapPin size={10} style={{ display: "inline", marginRight: 3 }} />
          {asset.physicalDescription}
        </div>
      )}
    </div>
    <div className={styles.assetCardActions}>
      <button className={styles.iconBtn} title="Edit" onClick={() => onEdit(asset)}>
        <Pencil size={13} />
      </button>
      <button className={styles.iconBtnDanger} title="Delete" onClick={() => onDelete(asset)}>
        <Trash2 size={13} />
      </button>
    </div>
  </div>
);

const Inventory = () => {
  const queryClient = useQueryClient();

  const [selectedLocation, setSelectedLocation] = useState<Location | null>(null);

  const [showLocationForm, setShowLocationForm] = useState(false);
  const [editingLocation, setEditingLocation] = useState<Location | null>(null);
  const [addChildParentId, setAddChildParentId] = useState<string | null>(null);

  const [showAssetForm, setShowAssetForm] = useState(false);
  const [editingAsset, setEditingAsset] = useState<Asset | null>(null);

  const { data: locations = [] } = useQuery({
    queryKey: ["locations"],
    queryFn: fetchLocations,
  });

  const { data: assets = [], isLoading: isLoadingAssets } = useQuery({
    queryKey: ["assets", selectedLocation?.id],
    queryFn: () => fetchAssetsByLocation(selectedLocation!.id),
    enabled: !!selectedLocation,
  });

  const { mutate: createLoc, isPending: isCreatingLoc } = useMutation({
    mutationFn: createLocation,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["locations"] });
      setShowLocationForm(false);
      setAddChildParentId(null);
    },
  });

  const { mutate: updateLoc, isPending: isUpdatingLoc } = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateLocationPayload }) => updateLocation(id, payload),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ["locations"] });
      setEditingLocation(null);
      if (selectedLocation?.id === updated.id) setSelectedLocation(updated);
    },
  });

  const { mutate: deleteLoc } = useMutation({
    mutationFn: (id: string) => deleteLocation(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ["locations"] });
      if (selectedLocation?.id === id) setSelectedLocation(null);
    },
  });

  const { mutate: createAssetMut, isPending: isCreatingAsset } = useMutation({
    mutationFn: createAsset,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["assets", selectedLocation?.id] });
      queryClient.invalidateQueries({ queryKey: ["locations"] });
      setShowAssetForm(false);
    },
  });

  const { mutate: updateAssetMut, isPending: isUpdatingAsset } = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: CreateAssetPayload }) => updateAsset(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["assets", selectedLocation?.id] });
      setEditingAsset(null);
    },
  });

  const { mutate: deleteAssetMut } = useMutation({
    mutationFn: (id: string) => deleteAsset(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["assets", selectedLocation?.id] });
      queryClient.invalidateQueries({ queryKey: ["locations"] });
    },
  });

  const rootLocations = locations.filter((l) => l.parentLocationId === null);

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Package size={20} className={styles.titleIcon} />
          <div>
            <h2 className={styles.pageTitle}>Assets &amp; Inventory</h2>
            <span className={styles.pageSubtitle}>
              Digital twin of your home — track every asset, room, and document
            </span>
          </div>
        </div>
      </div>

      <div className={styles.layout}>
        <aside className={styles.sidebar}>
          <div className={styles.sidebarHeader}>
            <span className={styles.sidebarTitle}>Locations</span>
            <button
              className={styles.addLocBtn}
              onClick={() => {
                setAddChildParentId(null);
                setShowLocationForm(true);
              }}
            >
              <Plus size={11} /> Add
            </button>
          </div>
          <div className={styles.locationList}>
            {rootLocations.length === 0 ? (
              <div className={styles.emptyState} style={{ padding: "2rem 1rem" }}>
                <Building2 size={28} className={styles.emptyIcon} />
                <span className={styles.emptyHint}>No locations yet. Add a room or area to get started.</span>
              </div>
            ) : (
              rootLocations.map((loc) => (
                <LocationTreeItem
                  key={loc.id}
                  loc={loc}
                  allLocations={locations}
                  selectedId={selectedLocation?.id ?? null}
                  onSelect={setSelectedLocation}
                  onEdit={(l) => setEditingLocation(l)}
                  onDelete={(l) => deleteLoc(l.id)}
                  onAddChild={(parentId) => {
                    setAddChildParentId(parentId);
                    setShowLocationForm(true);
                  }}
                />
              ))
            )}
          </div>
        </aside>

        <main className={styles.panel}>
          {selectedLocation ? (
            <>
              <div className={styles.panelHeader}>
                <div className={styles.panelHeaderLeft}>
                  <Building2 size={18} className={styles.panelIcon} />
                  <div>
                    <div className={styles.panelTitle}>{selectedLocation.name}</div>
                    {selectedLocation.accessNotes && (
                      <div className={styles.panelSubtitle}>
                        <MapPin size={10} style={{ display: "inline", marginRight: 3 }} />
                        {selectedLocation.accessNotes}
                      </div>
                    )}
                  </div>
                </div>
                <button className={styles.newButton} onClick={() => setShowAssetForm(true)}>
                  <Plus size={14} /> Add Asset
                </button>
              </div>
              <div className={styles.panelContent}>
                {isLoadingAssets ? (
                  <div className={styles.emptyState}>
                    <Loader2 size={22} className={styles.spin} style={{ opacity: 0.5 }} />
                  </div>
                ) : assets.length === 0 ? (
                  <div className={styles.emptyState}>
                    <Box size={32} className={styles.emptyIcon} />
                    <span className={styles.emptyTitle}>No assets here</span>
                    <span className={styles.emptyHint}>
                      Add the first asset in <strong>{selectedLocation.name}</strong> — anything from appliances to
                      documents.
                    </span>
                  </div>
                ) : (
                  assets.map((asset) => (
                    <AssetCard
                      key={asset.id}
                      asset={asset}
                      onEdit={(a) => setEditingAsset(a)}
                      onDelete={(a) => deleteAssetMut(a.id)}
                    />
                  ))
                )}
              </div>
            </>
          ) : (
            <div className={styles.emptyState} style={{ flex: 1 }}>
              <Building2 size={40} className={styles.emptyIcon} />
              <span className={styles.emptyTitle}>Select a location</span>
              <span className={styles.emptyHint}>Choose a room or area on the left to view and manage its assets.</span>
            </div>
          )}
        </main>
      </div>

      {(showLocationForm || editingLocation) && (
        <LocationForm
          initial={
            editingLocation
              ? {
                  name: editingLocation.name,
                  description: editingLocation.description ?? "",
                  accessNotes: editingLocation.accessNotes ?? "",
                }
              : undefined
          }
          parentId={editingLocation ? editingLocation.parentLocationId : addChildParentId}
          title={editingLocation ? "Edit Location" : addChildParentId ? "Add Sub-Location" : "Add Location"}
          isPending={editingLocation ? isUpdatingLoc : isCreatingLoc}
          onSubmit={(payload) => {
            if (editingLocation) {
              updateLoc({ id: editingLocation.id, payload });
            } else {
              createLoc(payload);
            }
          }}
          onCancel={() => {
            setShowLocationForm(false);
            setEditingLocation(null);
            setAddChildParentId(null);
          }}
        />
      )}

      {(showAssetForm || editingAsset) && selectedLocation && (
        <AssetForm
          initial={editingAsset ?? undefined}
          locationId={selectedLocation.id}
          title={editingAsset ? "Edit Asset" : "Add Asset"}
          isPending={editingAsset ? isUpdatingAsset : isCreatingAsset}
          onSubmit={(payload) => {
            if (editingAsset) {
              updateAssetMut({ id: editingAsset.id, payload });
            } else {
              createAssetMut(payload);
            }
          }}
          onCancel={() => {
            setShowAssetForm(false);
            setEditingAsset(null);
          }}
        />
      )}
    </div>
  );
};

export default Inventory;
