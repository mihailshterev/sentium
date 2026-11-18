import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EvalSuiteEditorView } from "./eval-suite-editor-view";
import { EvalSuitesView } from "./eval-suites-view";
import { client } from "@/shared/api/client";
import type { EvalSuite } from "../types/eval-suites";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return { ...actual, client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

const TOOLS = [
  { name: "knowledge_base_search", description: "Search", resource: "VectorDb", action: "Search", risk: "Low", requiresApproval: false },
];

const suite = (version: number, currentVersion = version): EvalSuite => ({
  id: "s-1",
  name: "Shopping",
  description: null,
  version,
  currentVersion,
  versionCreatedAt: "2026-11-17T10:00:00Z",
  cases: [
    {
      id: "c-1",
      name: "Groceries",
      prompt: version === 1 ? "What do we need?" : "What do we still need?",
      priorTurns: [],
      expectedAnswer: null,
      expectedToolCalls: [],
      cannedToolResults: [],
      checks: [{ type: "Relevance", threshold: null }],
    },
  ],
  createdAt: "2026-11-17T10:00:00Z",
  updatedAt: "2026-11-17T10:00:00Z",
});

function renderAt(path: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/evaluations" element={<EvalSuitesView />} />
          <Route path="/evaluations/suites/:suiteId" element={<EvalSuiteEditorView />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function mockGet(routes: Record<string, unknown>) {
  vi.mocked(client.get).mockImplementation((url: string) => {
    if (url === "/praxis/tools") {
      return Promise.resolve(TOOLS);
    }
    if (url in routes) {
      return Promise.resolve(routes[url]);
    }
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

describe("EvalSuiteEditorView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("creates a suite with a new case", async () => {
    mockGet({});
    vi.mocked(client.post).mockResolvedValue(suite(1));
    renderAt("/evaluations/suites/new");

    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Shopping" } });
    fireEvent.click(screen.getByRole("button", { name: "Add case" }));
    fireEvent.change(screen.getByLabelText("Prompt"), { target: { value: "What do we need?" } });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(client.post).toHaveBeenCalledWith("/probatio/suites", {
        name: "Shopping",
        description: null,
        cases: [
          {
            id: null,
            name: null,
            prompt: "What do we need?",
            priorTurns: [],
            expectedAnswer: null,
            expectedToolCalls: [],
            cannedToolResults: [],
            checks: [{ type: "Relevance", threshold: null }],
          },
        ],
      }),
    );
  });

  it("blocks saving while a case has problems", async () => {
    mockGet({});
    renderAt("/evaluations/suites/new");

    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Shopping" } });
    fireEvent.click(screen.getByRole("button", { name: "Add case" }));
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    expect(await screen.findByText("Fix the problems listed below first.")).toBeInTheDocument();
    expect(screen.getByText("Case 1: A case needs a prompt.")).toBeInTheDocument();
    expect(client.post).not.toHaveBeenCalled();
  });

  it("saves edits against the current version", async () => {
    mockGet({ "/probatio/suites/s-1": suite(1), "/probatio/suites/s-1/versions": [{ version: 1, caseCount: 1, createdAt: "" }] });
    vi.mocked(client.put).mockResolvedValue(suite(2));
    renderAt("/evaluations/suites/s-1");

    fireEvent.change(await screen.findByLabelText("Prompt"), { target: { value: "What do we still need?" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(client.put).toHaveBeenCalledWith(
        "/probatio/suites/s-1",
        expect.objectContaining({ baseVersion: 1, cases: [expect.objectContaining({ id: "c-1", prompt: "What do we still need?" })] }),
      ),
    );
  });

  it("shows an earlier version read-only", async () => {
    mockGet({
      "/probatio/suites/s-1/versions/1": suite(1, 2),
      "/probatio/suites/s-1/versions": [
        { version: 2, caseCount: 1, createdAt: "" },
        { version: 1, caseCount: 1, createdAt: "" },
      ],
    });
    renderAt("/evaluations/suites/s-1?version=1");

    expect(await screen.findByText(/viewing version 1, which is read-only/)).toBeInTheDocument();
    expect(screen.getByLabelText("Prompt")).toBeDisabled();
    expect(screen.getByLabelText("Prompt")).toHaveValue("What do we need?");
    expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
  });
});

describe("EvalSuitesView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("lists suites and deletes one after confirmation", async () => {
    mockGet({
      "/probatio/suites?page=1&pageSize=20": {
        items: [
          {
            id: "s-1",
            name: "Shopping",
            description: null,
            currentVersion: 2,
            caseCount: 3,
            createdAt: "2026-11-17T10:00:00Z",
            updatedAt: "2026-11-17T10:00:00Z",
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      },
    });
    vi.mocked(client.delete).mockResolvedValue(undefined);
    renderAt("/evaluations");

    expect(await screen.findByText("Shopping")).toBeInTheDocument();
    expect(screen.getByText("v2")).toBeInTheDocument();
    expect(screen.getByText("3 cases")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Delete Shopping" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete Suite" }));

    await waitFor(() => expect(client.delete).toHaveBeenCalledWith("/probatio/suites/s-1"));
  });
});
