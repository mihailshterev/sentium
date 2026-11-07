import { describe, it, expect, vi, beforeEach } from "vitest";
import * as modelsService from "./models.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe("models.service", () => {
  it("fetchModelCatalog reads the model catalog", async () => {
    const catalog = {
      defaultModel: "gemma-4-e4b",
      models: [{ id: "gemma-4-e4b", status: "loaded", contextLength: 16384 }],
    };
    vi.mocked(client.get).mockResolvedValueOnce(catalog);

    expect(await modelsService.fetchModelCatalog()).toEqual(catalog);
    expect(client.get).toHaveBeenCalledWith("/praxis/models");
  });

  it("loadModel posts the model id to the load endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce(undefined);
    await modelsService.loadModel("gemma-4-e4b");
    expect(client.post).toHaveBeenCalledWith("/praxis/models/load", { model: "gemma-4-e4b" });
  });

  it("unloadModel posts the model id to the unload endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce(undefined);
    await modelsService.unloadModel("gemma-4-e4b");
    expect(client.post).toHaveBeenCalledWith("/praxis/models/unload", { model: "gemma-4-e4b" });
  });
});
