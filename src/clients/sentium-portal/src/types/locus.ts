export interface Location {
  id: string;
  name: string;
  description: string | null;
  accessNotes: string | null;
  parentLocationId: string | null;
  assetCount: number;
  subLocationCount: number;
  createdAt: string;
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
  purchaseDate: string | null;
  lastServicedDate: string | null;
  warrantyInfo: string | null;
  isAgentAccessible: boolean;
  agentInstructions: string | null;
  locationId: string;
  locationName: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateLocationPayload {
  name: string;
  description?: string;
  accessNotes?: string;
  parentLocationId?: string | null;
}

export type UpdateLocationPayload = CreateLocationPayload;

export interface CreateAssetPayload {
  displayName: string;
  category?: string;
  physicalDescription?: string;
  instructions?: string;
  manufacturer?: string;
  modelNumber?: string;
  serialNumber?: string;
  purchaseDate?: string | null;
  lastServicedDate?: string | null;
  warrantyInfo?: string;
  isAgentAccessible: boolean;
  agentInstructions?: string;
  locationId: string;
}

export type UpdateAssetPayload = CreateAssetPayload;
