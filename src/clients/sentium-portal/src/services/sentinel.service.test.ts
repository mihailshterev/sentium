import { describe, it, expect, vi, beforeEach } from "vitest";
import * as sentinelService from "./sentinel.service";
import { client } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe("fetchAuditLog()", () => {
  it("defaults the count to 100", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await sentinelService.fetchAuditLog();
    expect(client.get).toHaveBeenCalledWith("/sentinel/policy/audit?count=100");
  });

  it("passes through an explicit count", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await sentinelService.fetchAuditLog(25);
    expect(client.get).toHaveBeenCalledWith("/sentinel/policy/audit?count=25");
  });
});

describe("fetchAuditByAgent()", () => {
  it("encodes the agent id and defaults count to 50", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await sentinelService.fetchAuditByAgent("agent/7");
    expect(client.get).toHaveBeenCalledWith("/sentinel/policy/audit/agent/agent%2F7?count=50");
  });
});

describe("fetchAuditStats()", () => {
  it("requests the audit stats endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({} as never);
    await sentinelService.fetchAuditStats();
    expect(client.get).toHaveBeenCalledWith("/sentinel/policy/audit/stats");
  });
});

describe("pdp settings", () => {
  it("fetchPdpSettings unwraps the envelope value", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({
      key: "pdp",
      value: { enabled: true },
      updatedAt: "x",
      updatedBy: null,
    } as never);

    const result = await sentinelService.fetchPdpSettings();

    expect(client.get).toHaveBeenCalledWith("/registry/settings/pdp");
    expect(result).toEqual({ enabled: true });
  });

  it("updatePdpSettings writes to the registry pdp key and unwraps the value", async () => {
    const payload = { enabled: false } as never;
    vi.mocked(client.put).mockResolvedValueOnce({
      key: "pdp",
      value: { enabled: false },
      updatedAt: "x",
      updatedBy: null,
    } as never);

    const result = await sentinelService.updatePdpSettings(payload);

    expect(client.put).toHaveBeenCalledWith("/registry/settings/pdp", payload);
    expect(result).toEqual({ enabled: false });
  });
});
