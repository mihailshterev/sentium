import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EvaluationView } from "./evaluation-view";
import { StartEvaluationDialog } from "../components/start-evaluation-dialog";
import { client } from "@/shared/api/client";
import type { Evaluation, EvaluationRepetition, EvaluationStatus } from "../types/evaluations";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return { ...actual, client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

const repetition = (passed: boolean): EvaluationRepetition => ({
  number: 1,
  passed,
  transcript: [
    { role: "user", text: "What is on the shopping list?" },
    { role: "assistant", text: "You still need: milk, eggs" },
  ],
  toolCalls: [{ toolName: "knowledge_base_search", arguments: { query: "milk" }, result: "milk, eggs" }],
  checks: [
    {
      type: "ContainsExpectedAnswer",
      status: passed ? "Passed" : "Failed",
      reason: passed ? null : "Expected 'milk'",
      score: null,
      threshold: null,
    },
    { type: "Relevance", status: "Passed", reason: "Answers the question.", score: 4, threshold: 4 },
  ],
  latencyMs: 1500,
  inputTokens: 100,
  outputTokens: 20,
  tokensPerSecond: 13.3,
  error: null,
});

const evaluation = (status: EvaluationStatus, judgeModel: string | null = "judge"): Evaluation => ({
  summary: {
    id: "e-1",
    suiteId: "s-1",
    suiteName: "Shopping",
    suiteVersion: 2,
    agentName: "ResearchAnalyst",
    benchmarkId: null,
    model: "gemma",
    judgeModel,
    repetitions: 1,
    status,
    error: null,
    caseCount: 2,
    completedCaseCount: status === "Running" ? 1 : 2,
    passedCaseCount: 1,
    createdAt: "2026-11-17T10:00:00Z",
    startedAt: "2026-11-17T10:00:01Z",
    finishedAt: null,
  },
  cases: [
    {
      caseId: "c-1",
      position: 0,
      name: "Shopping list",
      prompt: "What is on the shopping list?",
      checks: ["ContainsExpectedAnswer", "Relevance"],
      passed: true,
      passedRepetitions: 1,
      repetitions: [repetition(true)],
    },
    {
      caseId: "c-2",
      position: 1,
      name: null,
      prompt: "Say something",
      checks: ["ContainsExpectedAnswer"],
      passed: status === "Running" ? null : false,
      passedRepetitions: 0,
      repetitions: status === "Running" ? [] : [repetition(false)],
    },
  ],
  modelDeleted: false,
});

function renderView() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/evaluations/e-1"]}>
        <Routes>
          <Route path="/evaluations/:evaluationId" element={<EvaluationView />} />
          <Route path="/evaluations" element={<p>list</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("EvaluationView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows results per case with the repetition details", async () => {
    vi.mocked(client.get).mockResolvedValue(evaluation("Completed"));
    renderView();

    expect(await screen.findByText("Shopping v2")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
    expect(screen.getByText("passed")).toBeInTheDocument();
    expect(screen.getByText("failed")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Shopping list/ }));

    expect(screen.getByText("You still need: milk, eggs")).toBeInTheDocument();
    expect(screen.getByText('knowledge_base_search({"query":"milk"})')).toBeInTheDocument();
    expect(screen.getByText("Contains expected answer")).toBeInTheDocument();
    expect(screen.getByText(/score 4\/5 \(needs 4\)/)).toBeInTheDocument();
    expect(screen.getByText(/Answers the question\./)).toBeInTheDocument();
    expect(screen.getByText(/Judge: judge/)).toBeInTheDocument();
    expect(screen.queryByText(/scores its own answers/)).not.toBeInTheDocument();
  });

  it("warns when the Judge is the model under test", async () => {
    vi.mocked(client.get).mockResolvedValue(evaluation("Completed", "gemma"));
    renderView();

    expect(await screen.findByText(/scores its own answers/)).toBeInTheDocument();
  });

  it("shows progress and cancels a running Evaluation", async () => {
    vi.mocked(client.get).mockResolvedValue(evaluation("Running"));
    vi.mocked(client.post).mockResolvedValue(evaluation("Running").summary);
    renderView();

    expect(await screen.findByText("Evaluating… 1 of 2 cases done.")).toBeInTheDocument();
    expect(screen.getByText("pending")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(client.post).toHaveBeenCalledWith("/probatio/evaluations/e-1/cancel", {}));
  });

  it("deletes a finished Evaluation after confirmation", async () => {
    vi.mocked(client.get).mockResolvedValue(evaluation("Completed"));
    vi.mocked(client.delete).mockResolvedValue(undefined);
    renderView();

    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));
    fireEvent.click(await screen.findByRole("button", { name: "Delete Evaluation" }));

    await waitFor(() => expect(client.delete).toHaveBeenCalledWith("/probatio/evaluations/e-1"));
    expect(await screen.findByText("list")).toBeInTheDocument();
  });
});

describe("StartEvaluationDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("starts an Evaluation of the shown version and opens it", async () => {
    vi.mocked(client.get).mockImplementation((url: string) => {
      if (url === "/praxis/agents/roster") {
        return Promise.resolve([{ name: "ResearchAnalyst", description: "", kind: "BuiltIn", model: null }]);
      }
      return Promise.resolve({ defaultModel: "gemma", models: [] });
    });
    vi.mocked(client.post).mockResolvedValue({ id: "e-9" });
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={qc}>
        <MemoryRouter initialEntries={["/evaluations/suites/s-1"]}>
          <Routes>
            <Route
              path="/evaluations/suites/:suiteId"
              element={<StartEvaluationDialog open suiteId="s-1" suiteVersion={3} onClose={() => {}} />}
            />
            <Route path="/evaluations/:evaluationId" element={<p>opened</p>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await screen.findByRole("option", { name: "ResearchAnalyst" });
    fireEvent.change(screen.getByLabelText("Agent"), { target: { value: "ResearchAnalyst" } });
    fireEvent.change(screen.getByLabelText("Repetitions"), { target: { value: "3" } });
    fireEvent.click(screen.getByRole("button", { name: "Start" }));

    await waitFor(() =>
      expect(client.post).toHaveBeenCalledWith("/probatio/evaluations", {
        suiteId: "s-1",
        suiteVersion: 3,
        agentName: "ResearchAnalyst",
        model: null,
        repetitions: 3,
      }),
    );
    expect(await screen.findByText("opened")).toBeInTheDocument();
  });
});
