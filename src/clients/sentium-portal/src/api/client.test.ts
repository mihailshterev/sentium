import { describe, it, expect, vi, beforeEach } from "vitest";
import { BASE_URL, BFF_BASE, client } from "./client";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

describe("API client URL constants", () => {
  it("BASE_URL ends with /api", () => {
    expect(BASE_URL.endsWith("/api")).toBe(true);
  });

  it("BFF_BASE ends with /bff", () => {
    expect(BFF_BASE.endsWith("/bff")).toBe(true);
  });

  it("BASE_URL and BFF_BASE share the same base origin", () => {
    const baseWithoutSuffix = (url: string) => url.substring(0, url.lastIndexOf("/"));
    expect(baseWithoutSuffix(BASE_URL)).toBe(baseWithoutSuffix(BFF_BASE));
  });
});

describe("client.get()", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn()));

  it("calls fetch with GET method and correct URL", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "10" }),
      json: async () => ({ id: 1 }),
    } as unknown as Response);

    await client.get("/test");

    expect(fetch).toHaveBeenCalledWith(
      `${BASE_URL}/test`,
      expect.objectContaining({ method: "GET", credentials: "include" }),
    );
  });

  it("sets Content-Type: application/json header", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "10" }),
      json: async () => ({}),
    } as unknown as Response);

    await client.get("/headers-check");

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).headers).toMatchObject({
      "Content-Type": "application/json",
    });
  });

  it("returns parsed JSON body", async () => {
    const payload = { name: "Alice", id: 42 };
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "20" }),
      json: async () => payload,
    } as unknown as Response);

    const result = await client.get<typeof payload>("/data");
    expect(result).toEqual(payload);
  });

  it("returns empty object for 204 No Content", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Headers(),
      json: async () => {
        throw new Error("No body");
      },
    } as unknown as Response);

    const result = await client.get("/no-content");
    expect(result).toEqual({});
  });

  it("returns empty object when Content-Length is 0", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "0" }),
      json: async () => ({}),
    } as unknown as Response);

    const result = await client.get("/empty");
    expect(result).toEqual({});
  });
});

describe("client.post()", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn()));

  it("serialises body to JSON and uses POST method", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "5" }),
      json: async () => ({ ok: true }),
    } as unknown as Response);

    const body = { name: "test-agent", description: "desc", model: "llama3" };
    await client.post("/agents", body);

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).method).toBe("POST");
    expect((config as RequestInit).body).toBe(JSON.stringify(body));
  });
});

describe("client.put()", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn()));

  it("uses PUT method with serialised body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ "Content-Length": "5" }),
      json: async () => ({}),
    } as unknown as Response);

    await client.put("/agents/1", { name: "Updated" });

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).method).toBe("PUT");
  });
});

describe("client.delete()", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn()));

  it("uses DELETE method with no body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 204,
      headers: new Headers(),
      json: async () => {
        throw new Error("No body");
      },
    } as unknown as Response);

    await client.delete("/agents/1");

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).method).toBe("DELETE");
  });
});

describe("client 401 Unauthorized handling", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
    useAuthStore.setState({
      user: { sub: "u1", email: "u@example.com", name: "User", roles: [] },
      status: AUTH_STATUS.AUTHENTICATED,
    });
    (window.location as unknown as Record<string, string>).pathname = "/dashboard";
  });

  it("throws 'Session expired' error on 401", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 401,
      headers: new Headers(),
      json: async () => ({}),
    } as unknown as Response);

    await expect(client.get("/protected")).rejects.toThrow("Session expired");
  });

  it("redirects to BFF login on 401", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 401,
      headers: new Headers(),
      json: async () => ({}),
    } as unknown as Response);

    try {
      await client.get("/protected");
    } catch {
      /* expected */
    }

    expect(window.location.href).toContain("/bff/login");
  });

  it("clears auth store user on 401", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 401,
      headers: new Headers(),
      json: async () => ({}),
    } as unknown as Response);

    try {
      await client.get("/protected");
    } catch {
      /* expected */
    }

    expect(useAuthStore.getState().user).toBeNull();
  });
});

describe("client non-401 error handling", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn()));

  it("throws error with server message when response has 'message' field", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 422,
      headers: new Headers({ "Content-Length": "30" }),
      json: async () => ({ message: "Validation failed" }),
    } as unknown as Response);

    await expect(client.get("/validate")).rejects.toThrow("Validation failed");
  });

  it("falls back to generic HTTP error when body has no 'message'", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      headers: new Headers({ "Content-Length": "2" }),
      json: async () => ({}),
    } as unknown as Response);

    await expect(client.get("/fail")).rejects.toThrow("HTTP error! status: 500");
  });

  it("falls back to generic error when JSON parsing fails", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 503,
      headers: new Headers({ "Content-Length": "5" }),
      json: async () => {
        throw new SyntaxError("bad json");
      },
    } as unknown as Response);

    await expect(client.get("/bad-json")).rejects.toThrow("HTTP error! status: 503");
  });
});
