import { describe, it, expect, vi, beforeEach } from "vitest";
import { fetchSettingsGroup, updateSettingsGroup } from "./registry.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

const envelope = {
  key: "mcp",
  value: { enabled: true },
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: "admin",
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("registry settings service", () => {
  it("reads a settings group by key and returns the envelope", async () => {
    vi.mocked(client.get).mockResolvedValueOnce(envelope);

    const result = await fetchSettingsGroup("mcp");

    expect(client.get).toHaveBeenCalledWith("/registry/settings/mcp");
    expect(result).toEqual(envelope);
  });

  it("sends only the patch for a group", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(envelope);

    await updateSettingsGroup("calendar-user", { timeZone: null });

    expect(client.put).toHaveBeenCalledWith("/registry/settings/calendar-user", { timeZone: null });
  });
});
