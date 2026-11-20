import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { BackgroundJobsPanel } from "./background-jobs-panel";
import * as schedulerHooks from "../hooks/useScheduler";
import type { BackgroundJobsSnapshot } from "../types/scheduler";

const snapshot: BackgroundJobsSnapshot = {
  services: [
    { name: "Workflow run processor", type: "Sentium.Praxis.WorkflowRunProcessor", module: "Praxis", state: "Running", error: null },
    { name: "Note index worker", type: "Sentium.Memoria.NoteIndexWorker", module: "Memoria", state: "Faulted", error: "disk gone" },
  ],
  running: [
    { source: "Workflow runs", name: "Schedule: Morning briefing", detail: "Stream abc", ownerId: null, startedAt: "2025-01-31T05:30:00Z" },
  ],
};

const mockSnapshot = (value: Partial<ReturnType<typeof schedulerHooks.useBackgroundJobs>>) =>
  vi.spyOn(schedulerHooks, "useBackgroundJobs").mockReturnValue({
    snapshot: undefined,
    isLoading: false,
    error: null,
    ...value,
  } as ReturnType<typeof schedulerHooks.useBackgroundJobs>);

beforeEach(() => {
  vi.clearAllMocks();
});

describe("BackgroundJobsPanel", () => {
  it("lists the work running now and every background service with its state", () => {
    mockSnapshot({ snapshot });
    render(<BackgroundJobsPanel />);

    expect(within(screen.getByTestId("background-running")).getByText("Schedule: Morning briefing")).toBeInTheDocument();

    const services = screen.getByTestId("background-services");
    expect(within(services).getByText("Workflow run processor")).toBeInTheDocument();
    expect(within(services).getByText("Running")).toBeInTheDocument();
    expect(within(services).getByText("Faulted")).toBeInTheDocument();
    expect(within(services).getByText("disk gone")).toBeInTheDocument();
    expect(within(services).getByText(/1 service stopped with an error/i)).toBeInTheDocument();
  });

  it("says when nothing is running", () => {
    mockSnapshot({ snapshot: { ...snapshot, running: [] } });
    render(<BackgroundJobsPanel />);

    expect(screen.getByText(/nothing is running/i)).toBeInTheDocument();
  });

  it("reports a failed load", () => {
    mockSnapshot({ error: new Error("403") });
    render(<BackgroundJobsPanel />);

    expect(screen.getByRole("alert")).toHaveTextContent(/could not be loaded/i);
  });
});
