import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as workspacesService from "./workspaces.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

const okResponse = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as unknown as Response;

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("workspaces.service files", () => {
  it("listWorkspaceFiles builds the URL with and without a workspace filter", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    await workspacesService.listWorkspaceFiles();
    await workspacesService.listWorkspaceFiles("w 1");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files?workspaceId=w%201");
  });

  it("fetchWorkspaceFiles calls the workspace files endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([] as never);
    await workspacesService.fetchWorkspaceFiles("w1");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/w1/files");
  });

  it("deleteWorkspaceFile deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined as never);
    await workspacesService.deleteWorkspaceFile("f1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/workspaces/files/f1");
  });
});

describe("workspaces.service uploadWorkspaceFile()", () => {
  it("calls fetch with FormData (not JSON)", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(okResponse({ id: "file-1", name: "doc.pdf" }));

    const file = new File(["content"], "doc.pdf", { type: "application/pdf" });
    await workspacesService.uploadWorkspaceFile(file);

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).method).toBe("POST");
    expect((config as RequestInit).body).toBeInstanceOf(FormData);
  });

  it("appends workspaceId to FormData when provided", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(okResponse({}));

    await workspacesService.uploadWorkspaceFile(new File(["x"], "x.txt"), "ws-1");

    const [, config] = vi.mocked(fetch).mock.calls[0];
    const fd = (config as RequestInit).body as FormData;
    expect(fd.get("workspaceId")).toBe("ws-1");
  });

  it("returns the created file on success", async () => {
    vi.mocked(fetch).mockResolvedValueOnce(okResponse({ id: "f1" }));
    const result = await workspacesService.uploadWorkspaceFile(new File(["x"], "a.txt"), "w1");
    expect(result).toEqual({ id: "f1" });
  });

  it("throws 'Session expired' on 401 from upload endpoint", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({}) } as unknown as Response);
    await expect(workspacesService.uploadWorkspaceFile(new File(["x"], "x.txt"))).rejects.toThrow("Session expired.");
  });

  it("throws the API error message on failure", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => ({ error: "disk full" }),
    } as unknown as Response);
    await expect(workspacesService.uploadWorkspaceFile(new File(["x"], "a.txt"))).rejects.toThrow("disk full");
  });
});
