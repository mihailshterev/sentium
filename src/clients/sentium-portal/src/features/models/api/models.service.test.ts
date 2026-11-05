import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as modelsService from "./models.service";
import { BASE_URL, client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("models.service", () => {
  it("fetchModels maps the Ollama models to their names", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([
      { name: "llama3", modified_at: "", size: 0, digest: "", details: {} },
      { name: "mistral", modified_at: "", size: 0, digest: "", details: {} },
    ]);
    expect(await modelsService.fetchModels()).toEqual(["llama3", "mistral"]);
    expect(client.get).toHaveBeenCalledWith("/praxis/models");
  });

  it("fetchOllamaModels calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await modelsService.fetchOllamaModels();
    expect(client.get).toHaveBeenCalledWith("/praxis/models");
  });

  it("deleteOllamaModel encodes the model name", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({} as never);
    await modelsService.deleteOllamaModel("a/b");
    expect(client.delete).toHaveBeenCalledWith("/praxis/models?name=a%2Fb");
  });
});

describe("models.service pullModel()", () => {
  it("calls fetch with POST to correct URL", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    await modelsService.pullModel("llama3:latest");
    expect(fetch).toHaveBeenCalledWith(`${BASE_URL}/praxis/models/pull`, expect.objectContaining({ method: "POST" }));
  });

  it("serialises model name in body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    await modelsService.pullModel("mistral");
    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).body).toBe(JSON.stringify({ name: "mistral" }));
  });

  it("forwards AbortSignal to fetch", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    const controller = new AbortController();
    await modelsService.pullModel("llama3", controller.signal);
    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).signal).toBe(controller.signal);
  });
});
