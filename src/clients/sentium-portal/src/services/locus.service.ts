import type {
  Location,
  Asset,
  CreateLocationPayload,
  UpdateLocationPayload,
  CreateAssetPayload,
  UpdateAssetPayload,
} from "../types/locus";
import { client } from "../api/client";

const BASE = "/locus";

export const fetchLocations = () => client.get<Location[]>(`${BASE}/locations`);

export const fetchLocation = (id: string) => client.get<Location>(`${BASE}/locations/${id}`);

export const fetchSubLocations = (parentId: string) =>
  client.get<Location[]>(`${BASE}/locations/${parentId}/sub-locations`);

export const createLocation = (payload: CreateLocationPayload) => client.post<Location>(`${BASE}/locations`, payload);

export const updateLocation = (id: string, payload: UpdateLocationPayload) =>
  client.put<Location>(`${BASE}/locations/${id}`, payload);

export const deleteLocation = (id: string) => client.delete<void>(`${BASE}/locations/${id}`);

export const fetchAssets = () => client.get<Asset[]>(`${BASE}/assets`);

export const fetchAssetsByLocation = (locationId: string) =>
  client.get<Asset[]>(`${BASE}/assets/by-location/${locationId}`);

export const fetchAsset = (id: string) => client.get<Asset>(`${BASE}/assets/${id}`);

export const createAsset = (payload: CreateAssetPayload) => client.post<Asset>(`${BASE}/assets`, payload);

export const updateAsset = (id: string, payload: UpdateAssetPayload) =>
  client.put<Asset>(`${BASE}/assets/${id}`, payload);

export const deleteAsset = (id: string) => client.delete<void>(`${BASE}/assets/${id}`);
