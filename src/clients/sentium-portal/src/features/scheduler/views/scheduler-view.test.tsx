import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { SchedulerView as Scheduler } from "./scheduler-view";
import * as schedulerHooks from "@/features/scheduler/hooks/useScheduler";
import * as useRoleHook from "@/shared/auth/hooks/useRole";
import type { CronJobRecord } from "@/features/scheduler/types/scheduler";

vi.mock("../components/schedule-dialog", () => ({
  ScheduleDialog: ({ job }: { job?: CronJobRecord }) => <div role="dialog">{job ? `editing ${job.jobName}` : "creating"}</div>,
}));

vi.mock("../components/background-jobs-panel", () => ({
  BackgroundJobsPanel: () => <div>background jobs panel</div>,
}));

const scriptJob: CronJobRecord = {
  jobId: "job-1",
  jobName: "Nightly summary",
  agentId: "agent-1",
  language: "Python",
  cronExpression: "0 0 0 ? * *",
  nextRun: "2025-02-01 00:00:00 UTC",
  previousRun: "2025-01-31 00:00:00 UTC",
  status: "Normal",
  kind: "Script",
  workflowId: null,
  targetName: "",
  prompt: "",
  preApprovedTools: [],
  timeZone: "UTC",
  paused: false,
  lastRun: null,
  codeSnippet: "print('hi')",
};

const briefing: CronJobRecord = {
  ...scriptJob,
  jobId: "schedule-2",
  jobName: "Morning briefing",
  agentId: "TimelinePlanner",
  language: "",
  cronExpression: "0 30 7 ? * *",
  kind: "Agent",
  targetName: "TimelinePlanner",
  prompt: "Prepare my morning briefing",
  preApprovedTools: ["execute_code_sandbox"],
  timeZone: "Europe/Sofia",
  lastRun: { runId: "run-9", status: "Failed", at: "2025-01-31T05:30:00Z" },
  codeSnippet: undefined,
};

const deleteMutation = { mutateAsync: vi.fn(), isPending: false } as unknown as ReturnType<
  typeof schedulerHooks.useDeleteJobMutation
>;
const pauseResume = { mutate: vi.fn(), isPending: false } as unknown as ReturnType<
  typeof schedulerHooks.usePauseResumeMutation
>;

const role = (isSovereign: boolean) => ({
  roles: [isSovereign ? "Sovereign" : "Member"],
  highestRole: (isSovereign ? "Sovereign" : "Member") as "Sovereign" | "Member",
  isSovereign,
  isMemberOrAbove: true,
  isAuthenticated: true,
  hasRole: vi.fn(() => isSovereign),
});

const setJobs = (jobs: CronJobRecord[], isLoading = false, refetch = vi.fn()) => {
  vi.spyOn(schedulerHooks, "useSchedulerJobs").mockReturnValue({
    jobs,
    isLoading,
    error: null,
    refetch,
  } as unknown as ReturnType<typeof schedulerHooks.useSchedulerJobs>);
};

const renderView = () =>
  render(
    <MemoryRouter>
      <Scheduler />
    </MemoryRouter>,
  );

beforeEach(() => {
  vi.clearAllMocks();
  vi.spyOn(schedulerHooks, "useDeleteJobMutation").mockReturnValue(deleteMutation);
  vi.spyOn(schedulerHooks, "usePauseResumeMutation").mockReturnValue(pauseResume);
  vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(false));
  setJobs([scriptJob, briefing]);
});

describe("Scheduler states", () => {
  it("shows a loading message while loading with no schedules", () => {
    setJobs([], true);
    renderView();
    expect(screen.getByText(/loading schedules/i)).toBeInTheDocument();
  });

  it("shows an empty message when there are no schedules", () => {
    setJobs([]);
    renderView();
    expect(screen.getByText(/no schedules yet/i)).toBeInTheDocument();
  });

  it("renders each schedule with its kind, target and cadence in words", () => {
    renderView();
    const rows = screen.getAllByTestId("schedule-row");

    expect(within(rows[0]).getByText("Nightly summary")).toBeInTheDocument();
    expect(within(rows[0]).getByText("Script")).toBeInTheDocument();
    expect(within(rows[0]).getByText("Python script · agent-1")).toBeInTheDocument();

    expect(within(rows[1]).getByText("Agent")).toBeInTheDocument();
    expect(within(rows[1]).getByText("Every day at 07:30")).toBeInTheDocument();
    expect(within(rows[1]).getByText("Europe/Sofia")).toBeInTheDocument();
  });

  it("links the last run to its run history entry", () => {
    renderView();
    expect(screen.getByTestId("schedule-last-run")).toHaveAttribute("href", "/orchestration/runs/run-9");
    expect(screen.getByText("Failed")).toBeInTheDocument();
  });

  it("hides the background jobs tab from members", () => {
    renderView();
    expect(screen.queryByTestId("scheduler-tab-background")).not.toBeInTheDocument();
  });
});

describe("Scheduler interactions", () => {
  it("expands a row to reveal the prompt and pre-approvals", () => {
    renderView();
    fireEvent.click(screen.getByText("Morning briefing"));
    expect(screen.getByText("Prepare my morning briefing")).toBeInTheDocument();
    expect(screen.getByText("execute_code_sandbox")).toBeInTheDocument();
  });

  it("pauses an active schedule", () => {
    renderView();
    fireEvent.click(screen.getByRole("button", { name: "Pause Morning briefing" }));
    expect(pauseResume.mutate).toHaveBeenCalledWith({ jobId: "schedule-2", pause: true });
  });

  it("resumes a paused schedule", () => {
    setJobs([{ ...briefing, paused: true }]);
    renderView();
    fireEvent.click(screen.getByRole("button", { name: "Resume Morning briefing" }));
    expect(pauseResume.mutate).toHaveBeenCalledWith({ jobId: "schedule-2", pause: false });
  });

  it("opens the dialog to create or edit a schedule", () => {
    renderView();
    fireEvent.click(screen.getByTestId("schedule-new"));
    expect(screen.getByRole("dialog")).toHaveTextContent("creating");
  });

  it("opens the dialog on the schedule being edited", () => {
    renderView();
    fireEvent.click(screen.getByRole("button", { name: "Edit Morning briefing" }));
    expect(screen.getByRole("dialog")).toHaveTextContent("editing Morning briefing");
  });

  it("confirms before deleting a schedule", async () => {
    renderView();
    fireEvent.click(screen.getByRole("button", { name: "Delete Nightly summary" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/delete schedule\?/i)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByTestId("confirm-dialog-confirm"));
    await waitFor(() => expect(deleteMutation.mutateAsync).toHaveBeenCalledWith("job-1"));
  });

  it("refetches when the refresh button is clicked", () => {
    const refetch = vi.fn();
    setJobs([scriptJob], false, refetch);
    renderView();
    fireEvent.click(screen.getByRole("button", { name: /^refresh$/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it("shows the background jobs section to a Sovereign", () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    renderView();
    fireEvent.click(screen.getByTestId("scheduler-tab-background"));
    expect(screen.getByText("background jobs panel")).toBeInTheDocument();
  });
});
