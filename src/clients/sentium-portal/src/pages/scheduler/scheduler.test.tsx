import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import Scheduler from "./scheduler";
import * as schedulerHooks from "../../hooks/useScheduler";
import type { CronJobRecord } from "../../types/scheduler";

const job: CronJobRecord = {
  jobId: "job-1",
  jobName: "Nightly summary",
  agentId: "agent-1",
  language: "Python",
  cronExpression: "0 0 * * *",
  nextRun: "2025-02-01T00:00:00Z",
  previousRun: "2025-01-31T00:00:00Z",
  status: "Scheduled",
  codeSnippet: "print('hi')",
} as CronJobRecord;

const mutation = { mutateAsync: vi.fn(), isPending: false } as unknown as ReturnType<
  typeof schedulerHooks.useDeleteJobMutation
>;

const setJobs = (jobs: CronJobRecord[], isLoading = false) => {
  vi.spyOn(schedulerHooks, "useSchedulerJobs").mockReturnValue({
    jobs,
    isLoading,
    error: null,
    refetch: vi.fn(),
  } as unknown as ReturnType<typeof schedulerHooks.useSchedulerJobs>);
};

beforeEach(() => {
  vi.spyOn(schedulerHooks, "useDeleteJobMutation").mockReturnValue(mutation);
  setJobs([job]);
});

describe("Scheduler states", () => {
  it("shows a reading message while loading with no jobs", () => {
    setJobs([], true);
    render(<Scheduler />);
    expect(screen.getByText(/reading task signatures/i)).toBeInTheDocument();
  });

  it("shows an empty message when there are no jobs", () => {
    setJobs([]);
    render(<Scheduler />);
    expect(screen.getByText(/no automated cron jobs detected/i)).toBeInTheDocument();
  });

  it("renders job rows with their key fields", () => {
    render(<Scheduler />);
    expect(screen.getByText("Nightly summary")).toBeInTheDocument();
    expect(screen.getByText("agent-1")).toBeInTheDocument();
    expect(screen.getByText("0 0 * * *")).toBeInTheDocument();
    expect(screen.getByText("Scheduled")).toBeInTheDocument();
  });

  it("shows the active loop count in the stats", () => {
    render(<Scheduler />);
    expect(screen.getByText("1")).toBeInTheDocument();
  });
});

describe("Scheduler interactions", () => {
  it("expands a row to reveal job detail", () => {
    render(<Scheduler />);
    fireEvent.click(screen.getByText("Nightly summary"));
    expect(screen.getByText(/composite job key/i)).toBeInTheDocument();
  });

  it("opens the confirm dialog and deletes a job", async () => {
    render(<Scheduler />);
    fireEvent.click(screen.getByTitle("Terminate Scheduled Task"));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/terminate scheduled job/i)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByTestId("confirm-dialog-confirm"));
    await waitFor(() => expect(mutation.mutateAsync).toHaveBeenCalledWith({ agentId: "agent-1", jobId: "job-1" }));
  });

  it("refetches when the refresh button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(schedulerHooks, "useSchedulerJobs").mockReturnValue({
      jobs: [job],
      isLoading: false,
      error: null,
      refetch,
    } as unknown as ReturnType<typeof schedulerHooks.useSchedulerJobs>);
    render(<Scheduler />);
    fireEvent.click(screen.getByRole("button", { name: /refresh engine/i }));
    expect(refetch).toHaveBeenCalled();
  });
});
