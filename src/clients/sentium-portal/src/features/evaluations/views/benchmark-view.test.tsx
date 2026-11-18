import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BenchmarkView } from "./benchmark-view";
import { StartBenchmarkDialog } from "../components/start-benchmark-dialog";
import { client } from "@/shared/api/client";
import type { Benchmark } from "../types/benchmarks";
import type { Evaluation, EvaluationStatus } from "../types/evaluations";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return { ...actual, client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

const evaluation = (model: string, passed: boolean, latencyMs: number, status: EvaluationStatus = "Completed", modelDeleted = false): Evaluation => ({
  summary: {
    id: `e-${model}`,
    suiteId: "s-1",
    suiteName: "Shopping",
    suiteVersion: 2,
    agentName: "ResearchAnalyst",
    benchmarkId: "b-1",
    model,
    judgeModel: "judge",
    repetitions: 1,
    status,
    error: null,
    caseCount: 1,
    completedCaseCount: 1,
    passedCaseCount: passed ? 1 : 0,
    createdAt: "",
    startedAt: null,
    finishedAt: null,
  },
  cases: [
    {
      caseId: "c-1",
      position: 0,
      name: "Shopping list",
      prompt: "What is on the shopping list?",
      checks: ["ContainsExpectedAnswer", "Relevance"],
      passed,
      passedRepetitions: passed ? 1 : 0,
      repetitions: [
        {
          number: 1,
          passed,
          transcript: [],
          toolCalls: [],
          checks: [
            { type: "ContainsExpectedAnswer", status: passed ? "Passed" : "Failed", reason: null, score: null, threshold: null },
            { type: "Relevance", status: "Passed", reason: null, score: 4, threshold: 4 },
          ],
          latencyMs,
          inputTokens: 100,
          outputTokens: 20,
          tokensPerSecond: 20000 / latencyMs,
          error: null,
        },
      ],
    },
  ],
  modelDeleted,
});

const benchmark = (evaluations: Evaluation[], judgeModel = "judge"): Benchmark => ({
  id: "b-1",
  suiteId: "s-1",
  suiteName: "Shopping",
  suiteVersion: 2,
  agentName: "ResearchAnalyst",
  repetitions: 1,
  judgeModel,
  createdAt: "",
  evaluations,
});

function renderAt(path: string, element: React.ReactNode, routePath: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path={routePath} element={element} />
          <Route path="/evaluations/benchmarks/:benchmarkId" element={<p>benchmark opened</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("BenchmarkView", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("compares the models side by side and highlights the best", async () => {
    vi.mocked(client.get).mockResolvedValue(benchmark([evaluation("fast", true, 1000), evaluation("slow", false, 4000, "Completed", true)]));
    renderAt("/evaluations/benchmarks/b-1", <BenchmarkView />, "/evaluations/benchmarks/:benchmarkId");

    const overview = (await screen.findByText("Cases passed")).closest("table")!;
    expect(within(overview).getByRole("link", { name: "fast" })).toHaveAttribute("href", "/evaluations/e-fast");
    expect(within(overview).getByText("deleted")).toBeInTheDocument();
    expect(within(overview).getByText("1/1 · 100%").className).toMatch(/best/);
    expect(within(overview).getByText("0/1 · 0%").className).not.toMatch(/best/);
    expect(within(overview).getByText("1.0 s").className).toMatch(/best/);

    fireEvent.click(screen.getByRole("button", { name: /Shopping list/ }));

    expect(screen.getByText("Contains expected answer")).toBeInTheDocument();
    expect(screen.getAllByText(/score 4\.0/)).toHaveLength(2);
    expect(screen.queryByText(/scores its own answers/)).not.toBeInTheDocument();
  });

  it("warns when the Judge is one of the benchmarked models, and cancels while active", async () => {
    vi.mocked(client.get).mockResolvedValue(benchmark([evaluation("judge", true, 1000), evaluation("other", true, 2000, "Running")]));
    vi.mocked(client.post).mockResolvedValue({});
    renderAt("/evaluations/benchmarks/b-1", <BenchmarkView />, "/evaluations/benchmarks/:benchmarkId");

    expect(await screen.findByText(/scores its own answers/)).toBeInTheDocument();
    expect(screen.getByText(/1 of 2 done/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(client.post).toHaveBeenCalledWith("/probatio/benchmarks/b-1/cancel", {}));
  });
});

describe("StartBenchmarkDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("needs at least two models, then starts and opens the Benchmark", async () => {
    vi.mocked(client.get).mockImplementation((url: string) =>
      Promise.resolve(
        url === "/praxis/agents/roster"
          ? [{ name: "ResearchAnalyst", description: "", kind: "BuiltIn", model: null }]
          : { defaultModel: "gemma", models: [{ id: "gemma" }, { id: "qwen" }, { id: "phi" }] },
      ),
    );
    vi.mocked(client.post).mockResolvedValue({ id: "b-9" });
    renderAt(
      "/evaluations/suites/s-1",
      <StartBenchmarkDialog open suiteId="s-1" suiteVersion={2} onClose={() => {}} />,
      "/evaluations/suites/:suiteId",
    );

    await screen.findByRole("option", { name: "ResearchAnalyst" });
    fireEvent.change(screen.getByLabelText("Agent"), { target: { value: "ResearchAnalyst" } });
    fireEvent.click(await screen.findByRole("checkbox", { name: "gemma" }));
    expect(screen.getByRole("button", { name: "Start" })).toBeDisabled();

    fireEvent.click(screen.getByRole("checkbox", { name: "qwen" }));
    fireEvent.click(screen.getByRole("button", { name: "Start" }));

    await waitFor(() =>
      expect(client.post).toHaveBeenCalledWith("/probatio/benchmarks", {
        suiteId: "s-1",
        suiteVersion: 2,
        agentName: "ResearchAnalyst",
        models: ["gemma", "qwen"],
        repetitions: 1,
      }),
    );
    expect(await screen.findByText("benchmark opened")).toBeInTheDocument();
  });
});
