import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { InferencePanel } from "./inference-panel";
import { TimeSeriesChart } from "./time-series-chart";
import type { InferenceOverview } from "@/features/vigil/types/inference";

const useInferenceOverview = vi.fn();

vi.mock("@/features/vigil/hooks/useInferenceOverview", () => ({
  useInferenceOverview: (...args: unknown[]) => useInferenceOverview(...args),
}));

const overview: InferenceOverview = {
  range: "24h",
  stepMinutes: 15,
  from: "2026-11-20T10:00:00Z",
  to: "2026-11-21T10:00:00Z",
  models: [
    {
      server: "Chat",
      model: "gemma-4-e4b",
      status: "loaded",
      loadFailed: false,
      requestsProcessing: 1,
      requestsDeferred: 2,
      updatedAt: "2026-11-21T10:00:00Z",
    },
    {
      server: "Chat",
      model: "qwen-3",
      status: "unloaded",
      loadFailed: true,
      requestsProcessing: 0,
      requestsDeferred: 0,
      updatedAt: "2026-11-21T10:00:00Z",
    },
  ],
  series: [
    {
      server: "Chat",
      model: "gemma-4-e4b",
      points: [
        {
          at: "2026-11-21T09:30:00Z",
          promptTokensPerSecond: 850,
          generationTokensPerSecond: 31.5,
          promptTokens: 12_000,
          generatedTokens: 900,
          peakRequestsProcessing: 2,
          peakRequestsDeferred: 1,
          busyPercent: 40,
        },
        {
          at: "2026-11-21T09:45:00Z",
          promptTokensPerSecond: null,
          generationTokensPerSecond: null,
          promptTokens: 0,
          generatedTokens: 0,
          peakRequestsProcessing: 0,
          peakRequestsDeferred: 0,
          busyPercent: 0,
        },
      ],
    },
  ],
  usage: [
    {
      caller: "IntentCheck",
      model: "gemma-4-e4b",
      operation: "chat",
      requests: 12,
      errors: 1,
      inputTokens: 24_500,
      outputTokens: 300,
      averageDurationSeconds: 0.42,
      averageTimeToFirstTokenSeconds: null,
    },
  ],
  loads: [
    {
      server: "Chat",
      model: "qwen-3",
      startedAt: "2026-11-21T08:00:00Z",
      completedAt: "2026-11-21T08:00:30Z",
      durationSeconds: 30,
      succeeded: false,
    },
  ],
};

describe("InferencePanel", () => {
  beforeEach(() => {
    useInferenceOverview.mockReset();
    useInferenceOverview.mockReturnValue({ overview, isLoading: false, isUpdating: false, error: null });
  });

  it("shows each model with its live state and queue", () => {
    render(<InferencePanel />);

    const models = screen.getAllByTestId("inference-model");
    expect(models).toHaveLength(2);
    expect(within(models[0]).getByRole("heading", { name: "gemma-4-e4b" })).toBeInTheDocument();
    expect(within(models[0]).getByText("1 processing · 2 waiting")).toBeInTheDocument();
    expect(within(models[1]).getByText("load failed")).toBeInTheDocument();
    expect(within(models[1]).getByText("No samples in this range yet.")).toBeInTheDocument();
  });

  it("names callers and lists usage and model loads", () => {
    render(<InferencePanel />);

    const usage = screen.getByRole("region", { name: "Usage by caller" });
    expect(within(usage).getByRole("rowheader", { name: "Sentinel intent checks" })).toBeInTheDocument();
    expect(within(usage).getByText("24.5k")).toBeInTheDocument();
    expect(within(usage).getByText("420 ms")).toBeInTheDocument();
    expect(screen.getByText("failed")).toBeInTheDocument();
  });

  it("switches a model to its table view", async () => {
    render(<InferencePanel />);
    const model = screen.getAllByTestId("inference-model")[0];

    await userEvent.click(within(model).getByRole("button", { name: "Table view" }));

    const table = within(model).getByRole("table");
    expect(within(table).getByText("31.5")).toBeInTheDocument();
    expect(within(model).getByRole("button", { name: "Chart view" })).toHaveAttribute("aria-pressed", "true");
  });

  it("asks for the selected range", async () => {
    render(<InferencePanel />);

    await userEvent.click(screen.getByRole("tab", { name: "7d" }));

    expect(useInferenceOverview).toHaveBeenLastCalledWith("7d");
  });

  it("explains when nothing has reported yet", () => {
    useInferenceOverview.mockReturnValue({
      overview: { ...overview, models: [], series: [], usage: [], loads: [] },
      isLoading: false,
      isUpdating: false,
      error: null,
    });

    render(<InferencePanel />);

    expect(screen.getByText(/No model has reported yet/)).toBeInTheDocument();
    expect(screen.getByText("No requests in this range.")).toBeInTheDocument();
  });
});

describe("TimeSeriesChart", () => {
  it("steps through buckets with the arrow keys and shows each value", () => {
    render(
      <TimeSeriesChart
        title="Speed"
        points={[
          { at: 0, value: 12 },
          { at: 60_000, value: null },
        ]}
        from={0}
        to={60_000}
        stepMs={60_000}
        formatValue={(v) => `${v} t/s`}
        describe={() => [{ label: "busy", value: "40%" }]}
      />,
    );
    const plot = screen.getByRole("group", { name: "Speed" });

    fireEvent.keyDown(plot, { key: "ArrowRight" });
    expect(screen.getByText("12 t/s")).toBeInTheDocument();
    expect(screen.getByText("40%")).toBeInTheDocument();

    fireEvent.keyDown(plot, { key: "ArrowRight" });
    expect(screen.getByText("no work")).toBeInTheDocument();

    fireEvent.keyDown(plot, { key: "Escape" });
    expect(screen.queryByText("no work")).not.toBeInTheDocument();
  });
});
