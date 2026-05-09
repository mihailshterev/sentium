import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  fetchLocations,
  fetchLocation,
  fetchSubLocations,
  createLocation,
  updateLocation,
  deleteLocation,
  fetchAssets,
  fetchAssetsByLocation,
  fetchAsset,
  createAsset,
  updateAsset,
  deleteAsset,
} from "./locus.service";
import { client } from "../api/client";

vi.mock("../api/client", () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

beforeEach(() => {
  vi.mocked(client.get).mockReset();
  vi.mocked(client.post).mockReset();
  vi.mocked(client.put).mockReset();
  vi.mocked(client.delete).mockReset();
});

const mockLocation = {
  id: "loc-1",
  name: "Server Room",
  description: null,
  accessNotes: null,
  parentLocationId: null,
  assetCount: 3,
  subLocationCount: 1,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockAsset = {
  id: "asset-1",
  displayName: "Dell PowerEdge R740",
  category: "Server",
  physicalDescription: null,
  instructions: null,
  manufacturer: "Dell",
  modelNumber: "R740",
  serialNumber: "SN123",
  purchaseDate: null,
  lastServicedDate: null,
  warrantyInfo: null,
  isAgentAccessible: true,
  agentInstructions: null,
  locationId: "loc-1",
  locationName: "Server Room",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

describe("locus.service location reads", () => {
  it("fetchLocations calls GET /locus/locations", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([mockLocation]);
    const result = await fetchLocations();
    expect(client.get).toHaveBeenCalledWith("/locus/locations");
    expect(result).toEqual([mockLocation]);
  });

  it("fetchLocation calls GET /locus/locations/:id", async () => {
    vi.mocked(client.get).mockResolvedValueOnce(mockLocation);
    const result = await fetchLocation("loc-1");
    expect(client.get).toHaveBeenCalledWith("/locus/locations/loc-1");
    expect(result).toEqual(mockLocation);
  });

  it("fetchSubLocations calls GET /locus/locations/:id/sub-locations", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await fetchSubLocations("loc-1");
    expect(client.get).toHaveBeenCalledWith("/locus/locations/loc-1/sub-locations");
  });
});

describe("locus.service location mutations", () => {
  it("createLocation POSTs payload to /locus/locations", async () => {
    const payload = { name: "Rack A", description: undefined, accessNotes: undefined, parentLocationId: undefined };
    vi.mocked(client.post).mockResolvedValueOnce(mockLocation);
    await createLocation(payload);
    expect(client.post).toHaveBeenCalledWith("/locus/locations", payload);
  });

  it("updateLocation PUTs payload to /locus/locations/:id", async () => {
    const payload = {
      name: "Rack A Updated",
      description: undefined,
      accessNotes: undefined,
      parentLocationId: undefined,
    };
    vi.mocked(client.put).mockResolvedValueOnce(mockLocation);
    await updateLocation("loc-1", payload);
    expect(client.put).toHaveBeenCalledWith("/locus/locations/loc-1", payload);
  });

  it("deleteLocation DELETEs /locus/locations/:id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await deleteLocation("loc-1");
    expect(client.delete).toHaveBeenCalledWith("/locus/locations/loc-1");
  });
});

describe("locus.service asset reads", () => {
  it("fetchAssets calls GET /locus/assets", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([mockAsset]);
    const result = await fetchAssets();
    expect(client.get).toHaveBeenCalledWith("/locus/assets");
    expect(result).toEqual([mockAsset]);
  });

  it("fetchAssetsByLocation calls GET /locus/assets/by-location/:id", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([mockAsset]);
    await fetchAssetsByLocation("loc-1");
    expect(client.get).toHaveBeenCalledWith("/locus/assets/by-location/loc-1");
  });

  it("fetchAsset calls GET /locus/assets/:id", async () => {
    vi.mocked(client.get).mockResolvedValueOnce(mockAsset);
    const result = await fetchAsset("asset-1");
    expect(client.get).toHaveBeenCalledWith("/locus/assets/asset-1");
    expect(result).toEqual(mockAsset);
  });
});

describe("locus.service asset mutations", () => {
  it("createAsset POSTs payload to /locus/assets", async () => {
    const payload = { displayName: "Switch", locationId: "loc-1", isAgentAccessible: false };
    vi.mocked(client.post).mockResolvedValueOnce(mockAsset);
    await createAsset(payload);
    expect(client.post).toHaveBeenCalledWith("/locus/assets", payload);
  });

  it("updateAsset PUTs payload to /locus/assets/:id", async () => {
    const payload = { displayName: "Switch v2", locationId: "loc-1", isAgentAccessible: true };
    vi.mocked(client.put).mockResolvedValueOnce(mockAsset);
    await updateAsset("asset-1", payload);
    expect(client.put).toHaveBeenCalledWith("/locus/assets/asset-1", payload);
  });

  it("deleteAsset DELETEs /locus/assets/:id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await deleteAsset("asset-1");
    expect(client.delete).toHaveBeenCalledWith("/locus/assets/asset-1");
  });
});
