import { describe, it, expect, vi, beforeEach } from "vitest";
import * as registryService from "./registry.service";
import { client } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

const harnessEnvelope = {
  key: "harness",
  value: { foo: "bar" } as never,
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: "admin",
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("fetchSettings()", () => {
  it("reads the harness settings and maps the envelope to Settings", async () => {
    vi.mocked(client.get).mockResolvedValueOnce(harnessEnvelope);
    const result = await registryService.fetchSettings();

    expect(client.get).toHaveBeenCalledWith("/registry/settings/harness");
    expect(result).toEqual({
      harness: harnessEnvelope.value,
      updatedAt: harnessEnvelope.updatedAt,
      updatedBy: harnessEnvelope.updatedBy,
    });
  });
});

describe("updateSettings()", () => {
  it("writes only the harness payload and maps the response", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(harnessEnvelope);
    const result = await registryService.updateSettings({ harness: harnessEnvelope.value });

    expect(client.put).toHaveBeenCalledWith("/registry/settings/harness", harnessEnvelope.value);
    expect(result.updatedBy).toBe("admin");
  });
});

describe("ollama settings", () => {
  const ollamaEnvelope = {
    key: "ollama",
    value: { defaultModel: "gemma", agentTemperature: 0.7, agentContextWindow: 4096 },
    updatedAt: "2025-01-01T00:00:00Z",
    updatedBy: null,
  };

  it("fetchOllamaSettings reads the ollama key", async () => {
    vi.mocked(client.get).mockResolvedValueOnce(ollamaEnvelope);
    const result = await registryService.fetchOllamaSettings();
    expect(client.get).toHaveBeenCalledWith("/registry/settings/ollama");
    expect(result.value.defaultModel).toBe("gemma");
  });

  it("updateOllamaSettings writes the ollama key", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(ollamaEnvelope);
    await registryService.updateOllamaSettings(ollamaEnvelope.value);
    expect(client.put).toHaveBeenCalledWith("/registry/settings/ollama", ollamaEnvelope.value);
  });
});
