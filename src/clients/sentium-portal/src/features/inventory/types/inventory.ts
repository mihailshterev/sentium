export type KnowledgeStatus = "NotShared" | "Pending" | "Synced" | "Failed";

export type AttachmentKind = "Photo" | "Manual" | "Receipt" | "Other";

export type AssetSort = "Name" | "Updated" | "Created";

export interface InventoryLocation {
  id: string;
  name: string;
  description: string | null;
  accessNotes: string | null;
  parentLocationId: string | null;
  path: string;
  depth: number;
  assetCount: number;
  subtreeAssetCount: number;
  childCount: number;
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface LocationTreeNode extends InventoryLocation {
  children: LocationTreeNode[];
}

export interface SaveLocationPayload {
  name: string;
  description: string | null;
  accessNotes: string | null;
  parentLocationId: string | null;
}

export interface LocationCrumb {
  id: string;
  name: string;
}

export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  kind: AttachmentKind;
  isImage: boolean;
  contentUrl: string;
  createdAt: string;
  createdBy: string | null;
}

export interface AssetSummary {
  id: string;
  displayName: string;
  category: string | null;
  tags: string[];
  manufacturer: string | null;
  modelNumber: string | null;
  locationId: string;
  locationPath: string;
  isAgentAccessible: boolean;
  knowledgeStatus: KnowledgeStatus;
  attachmentCount: number;
  coverImageUrl: string | null;
  updatedAt: string;
}

export interface Asset {
  id: string;
  displayName: string;
  category: string | null;
  physicalDescription: string | null;
  instructions: string | null;
  manufacturer: string | null;
  modelNumber: string | null;
  serialNumber: string | null;
  /** yyyy-MM-dd */
  purchaseDate: string | null;
  /** yyyy-MM-dd */
  lastServicedDate: string | null;
  warrantyInfo: string | null;
  isAgentAccessible: boolean;
  agentInstructions: string | null;
  tags: string[];
  locationId: string;
  locationPath: string;
  locationCrumbs: LocationCrumb[];
  knowledgeStatus: KnowledgeStatus;
  knowledgeError: string | null;
  knowledgeSyncedAt: string | null;
  attachments: Attachment[];
  createdAt: string;
  updatedAt: string;
  createdBy: string | null;
  updatedBy: string | null;
}

export interface SaveAssetPayload {
  displayName: string;
  locationId: string;
  category: string | null;
  physicalDescription: string | null;
  instructions: string | null;
  manufacturer: string | null;
  modelNumber: string | null;
  serialNumber: string | null;
  purchaseDate: string | null;
  lastServicedDate: string | null;
  warrantyInfo: string | null;
  isAgentAccessible: boolean;
  agentInstructions: string | null;
  tags: string[];
}

export interface AssetFilters {
  q?: string;
  locationId?: string | null;
  includeSubLocations?: boolean;
  category?: string | null;
  tags?: string[];
  agentAccessible?: boolean | null;
  sort?: AssetSort;
}

export interface TagCount {
  name: string;
  count: number;
}
