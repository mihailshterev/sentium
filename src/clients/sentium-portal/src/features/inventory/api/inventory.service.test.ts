import { beforeEach, describe, expect, it, vi } from "vitest";
import { client } from "@/shared/api/client";
import * as api from "./inventory.service";

vi.mock("@/shared/api/client", async (importOriginal) => ({
  ...(await importOriginal()),
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(), postForm: vi.fn() },
}));

describe("inventory service", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lists and saves locations under /locus/locations", async () => {
    await api.fetchLocations();
    await api.createLocation({ name: "Garage", parentLocationId: "home", description: null, accessNotes: null });
    await api.updateLocation("loc-1", { name: "Shed", parentLocationId: null, description: null, accessNotes: null });

    expect(client.get).toHaveBeenCalledWith("/locus/locations");
    expect(client.post).toHaveBeenCalledWith("/locus/locations", expect.objectContaining({ name: "Garage" }));
    expect(client.put).toHaveBeenCalledWith("/locus/locations/loc-1", expect.objectContaining({ name: "Shed" }));
  });

  it("passes the move target when deleting a non-empty location", async () => {
    await api.deleteLocation("loc-1");
    await api.deleteLocation("loc-2", "home");

    expect(client.delete).toHaveBeenNthCalledWith(1, "/locus/locations/loc-1");
    expect(client.delete).toHaveBeenNthCalledWith(2, "/locus/locations/loc-2?moveContentsTo=home");
  });

  it("builds the asset query with one tag parameter per tag", () => {
    const query = api.buildAssetQuery(
      {
        q: "  drill ",
        locationId: "loc-1",
        includeSubLocations: false,
        category: "Tools",
        tags: ["power", "cordless"],
        agentAccessible: false,
        sort: "Updated",
      },
      2,
      30,
    );

    const params = new URLSearchParams(query);
    expect(params.get("q")).toBe("drill");
    expect(params.get("locationId")).toBe("loc-1");
    expect(params.get("includeSubLocations")).toBe("false");
    expect(params.get("category")).toBe("Tools");
    expect(params.getAll("tag")).toEqual(["power", "cordless"]);
    expect(params.get("agentAccessible")).toBe("false");
    expect(params.get("sort")).toBe("Updated");
    expect(params.get("page")).toBe("2");
    expect(params.get("pageSize")).toBe("30");
  });

  it("omits defaults from the asset query", () => {
    const params = new URLSearchParams(api.buildAssetQuery({ q: "", sort: "Name", agentAccessible: null }, 1, 30));

    expect([...params.keys()]).toEqual(["page", "pageSize"]);
  });

  it("uploads attachments as multipart form data", async () => {
    const file = new File(["png"], "front.png", { type: "image/png" });

    await api.uploadAttachment("asset-1", file, "Photo");

    expect(client.postForm).toHaveBeenCalledWith("/locus/assets/asset-1/attachments", expect.any(FormData));
    const form = vi.mocked(client.postForm).mock.calls[0][1];
    expect(form.get("file")).toBe(file);
    expect(form.get("kind")).toBe("Photo");
  });

  it("hits the asset, taxonomy and sync endpoints", async () => {
    await api.fetchAsset("a 1");
    await api.deleteAsset("a1");
    await api.requestKnowledgeSync("a1");
    await api.deleteAttachment("a1", "att1");
    await api.fetchTags("po");
    await api.fetchCategories();

    expect(client.get).toHaveBeenCalledWith("/locus/assets/a%201");
    expect(client.delete).toHaveBeenCalledWith("/locus/assets/a1");
    expect(client.post).toHaveBeenCalledWith("/locus/assets/a1/knowledge-sync", undefined);
    expect(client.delete).toHaveBeenCalledWith("/locus/assets/a1/attachments/att1");
    expect(client.get).toHaveBeenCalledWith("/locus/tags?prefix=po");
    expect(client.get).toHaveBeenCalledWith("/locus/categories");
  });

  it("builds download links from the content url", () => {
    expect(api.downloadUrl({ contentUrl: "/api/locus/assets/a/attachments/b/content" } as never)).toBe(
      "/api/locus/assets/a/attachments/b/content?download=true",
    );
  });
});
