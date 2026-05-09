import { describe, it, expect, vi, beforeEach } from "vitest";
import { fetchNetworkEvents } from "./sentinel.service";
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
});

describe("sentinel.service – fetchNetworkEvents()", () => {
  it("calls client.get with /sentinel/events/network?count=100 by default", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await fetchNetworkEvents();
    expect(client.get).toHaveBeenCalledWith("/sentinel/events/network?count=100");
  });

  it("uses the provided count parameter", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await fetchNetworkEvents(50);
    expect(client.get).toHaveBeenCalledWith("/sentinel/events/network?count=50");
  });

  it("returns the event array from the API", async () => {
    const event = {
      id: "ev-1",
      source: "firewall",
      action: "block",
      timestamp: "2025-01-01T00:00:00Z",
      origH: "10.0.0.1",
      respH: "8.8.8.8",
      proto: "tcp",
      service: "http",
      mlScore: "0.95",
    };
    vi.mocked(client.get).mockResolvedValueOnce([event]);
    const result = await fetchNetworkEvents();
    expect(result).toEqual([event]);
  });

  it("propagates errors thrown by client.get", async () => {
    vi.mocked(client.get).mockRejectedValueOnce(new Error("Network error"));
    await expect(fetchNetworkEvents()).rejects.toThrow("Network error");
  });
});
