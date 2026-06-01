import { describe, it, expect, vi, beforeEach } from "vitest";
import * as sandboxService from "./sandbox.service";
import { client } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
  };
});

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

describe("fetchExecutions()", () => {
  it("builds a paged URL with only page and pageSize when no filters", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await sandboxService.fetchExecutions({ page: 1, pageSize: 20 });
    expect(client.get).toHaveBeenCalledWith("/sandbox/executions?page=1&pageSize=20");
  });

  it("appends status, language and search when provided", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await sandboxService.fetchExecutions({
      page: 2,
      pageSize: 50,
      status: "Failed",
      language: "Python",
      search: "agent-7",
    });
    expect(client.get).toHaveBeenCalledWith(
      "/sandbox/executions?page=2&pageSize=50&status=Failed&language=Python&search=agent-7",
    );
  });

  it("omits a blank search term", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await sandboxService.fetchExecutions({ page: 1, pageSize: 20, search: "   " });
    expect(client.get).toHaveBeenCalledWith("/sandbox/executions?page=1&pageSize=20");
  });
});

describe("fetchExecution()", () => {
  it("calls client.get with the job id in the path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await sandboxService.fetchExecution("job-1");
    expect(client.get).toHaveBeenCalledWith("/sandbox/executions/job-1");
  });
});

describe("fetchSandboxStats()", () => {
  it("calls client.get with the stats endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await sandboxService.fetchSandboxStats();
    expect(client.get).toHaveBeenCalledWith("/sandbox/executions/stats");
  });
});

describe("getArtifactUrl()", () => {
  it("builds an absolute artifact URL from the download path", () => {
    const url = sandboxService.getArtifactUrl("abc/result.png");
    expect(url).toContain("/sandbox/artifacts/abc/result.png");
  });
});
