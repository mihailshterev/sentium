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

/** A stand-in XMLHttpRequest that answers each send with the scripted status and body. */
class FakeXhr {
  static next: { status: number; body: string } = { status: 201, body: "{}" };
  static last: FakeXhr | null = null;

  method = "";
  url = "";
  withCredentials = false;
  status = 0;
  responseText = "";
  sent: FormData | null = null;
  upload: { onprogress: ((e: { lengthComputable: boolean; loaded: number; total: number }) => void) | null } = {
    onprogress: null,
  };
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;

  open(method: string, url: string) {
    this.method = method;
    this.url = url;
  }

  send(body: FormData) {
    FakeXhr.last = this;
    this.sent = body;
    this.upload.onprogress?.({ lengthComputable: true, loaded: 5, total: 10 });
    this.status = FakeXhr.next.status;
    this.responseText = FakeXhr.next.body;
    queueMicrotask(() => this.onload?.());
  }
}

describe("workspaces.service uploadWorkspaceFile()", () => {
  beforeEach(() => {
    FakeXhr.next = { status: 201, body: JSON.stringify({ id: "f1" }) };
    FakeXhr.last = null;
    vi.stubGlobal("XMLHttpRequest", FakeXhr);
  });

  it("posts multipart form data with credentials and reports progress", async () => {
    const onProgress = vi.fn();
    const file = new File(["content"], "doc.pdf", { type: "application/pdf" });

    await workspacesService.uploadWorkspaceFile(file, undefined, onProgress);

    const xhr = FakeXhr.last!;
    expect(xhr.method).toBe("POST");
    expect(xhr.url).toMatch(/\/praxis\/workspaces\/files$/);
    expect(xhr.withCredentials).toBe(true);
    expect(xhr.sent).toBeInstanceOf(FormData);
    expect(xhr.sent!.get("workspaceId")).toBeNull();
    expect(onProgress).toHaveBeenCalledWith(0.5);
  });

  it("appends workspaceId to the form when provided", async () => {
    await workspacesService.uploadWorkspaceFile(new File(["x"], "x.txt"), "ws-1");
    expect(FakeXhr.last!.sent!.get("workspaceId")).toBe("ws-1");
  });

  it("returns the created file on success", async () => {
    const result = await workspacesService.uploadWorkspaceFile(new File(["x"], "a.txt"), "w1");
    expect(result).toEqual({ id: "f1" });
  });

  it("rejects with 'Session expired' on 401", async () => {
    FakeXhr.next = { status: 401, body: "" };
    await expect(workspacesService.uploadWorkspaceFile(new File(["x"], "x.txt"))).rejects.toThrow("Session expired");
  });

  it("rejects with the problem details' detail on failure", async () => {
    FakeXhr.next = {
      status: 400,
      body: JSON.stringify({ title: "Bad Request", detail: "File exceeds the 100 MB size limit." }),
    };
    await expect(workspacesService.uploadWorkspaceFile(new File(["x"], "a.txt"))).rejects.toThrow(
      "File exceeds the 100 MB size limit.",
    );
  });

  it("falls back to the status when the error body is unreadable", async () => {
    FakeXhr.next = { status: 502, body: "<html>bad gateway</html>" };
    await expect(workspacesService.uploadWorkspaceFile(new File(["x"], "a.txt"))).rejects.toThrow(
      "Upload failed (502).",
    );
  });
});

describe("workspaces.service file endpoints", () => {
  it("builds the content, text, reindex and rules URLs", async () => {
    vi.mocked(client.get).mockResolvedValue({} as never);
    vi.mocked(client.post).mockResolvedValue(undefined as never);

    expect(workspacesService.workspaceFileContentUrl("f1")).toMatch(/\/praxis\/workspaces\/files\/f1\/content$/);
    await workspacesService.fetchWorkspaceFileText("f1", 500);
    await workspacesService.reindexWorkspaceFile("f1");
    await workspacesService.fetchUploadRules();
    await workspacesService.fetchWorkspace("w1");

    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files/f1/text?maxChars=500");
    expect(client.post).toHaveBeenCalledWith("/praxis/workspaces/files/f1/reindex", undefined);
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files/rules");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/w1");
  });
});
