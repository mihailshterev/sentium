import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ScheduleDialog } from "./schedule-dialog";
import * as schedulerHooks from "../hooks/useScheduler";
import * as rosterHooks from "@/features/agents/hooks/useAgentRoster";
import * as workflowHooks from "@/features/workflows/hooks/useWorkflows";
import type { CronJobRecord } from "../types/scheduler";

const save = { mutateAsync: vi.fn(), isPending: false } as unknown as ReturnType<
  typeof schedulerHooks.useSaveScheduleMutation
>;

const gatedTool = {
  name: "execute_code_sandbox",
  description: "Runs code in the sandbox.",
  resource: "Code",
  action: "Execute",
  risk: "High",
  requiresApproval: true,
};

const briefing: CronJobRecord = {
  jobId: "schedule-2",
  jobName: "Morning briefing",
  agentId: "TimelinePlanner",
  language: "",
  cronExpression: "0 30 7 ? * *",
  previousRun: null,
  nextRun: null,
  status: "Normal",
  kind: "Agent",
  workflowId: null,
  targetName: "TimelinePlanner",
  prompt: "Brief me",
  preApprovedTools: ["execute_code_sandbox"],
  timeZone: "Europe/Sofia",
  paused: false,
  lastRun: null,
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.spyOn(schedulerHooks, "useSaveScheduleMutation").mockReturnValue(save);
  vi.spyOn(schedulerHooks, "useScheduleTemplates").mockReturnValue([
    {
      id: "morning-briefing",
      name: "Morning briefing",
      description: "Every day at 07:30.",
      agentName: "TimelinePlanner",
      cronExpression: "0 30 7 ? * *",
      prompt: "Prepare my morning briefing.",
    },
  ]);
  vi.spyOn(schedulerHooks, "usePreApprovableTools").mockImplementation((target) => ({
    tools: target ? [gatedTool] : [],
    isLoading: false,
    isError: false,
  }));
  vi.spyOn(rosterHooks, "useAgentRoster").mockReturnValue({
    roster: [
      { name: "GeneralAssistant", description: "", kind: "BuiltIn" },
      { name: "TimelinePlanner", description: "", kind: "BuiltIn" },
    ],
    isLoading: false,
    isError: false,
  });
  vi.spyOn(workflowHooks, "useWorkflows").mockReturnValue({ workflows: [] } as unknown as ReturnType<
    typeof workflowHooks.useWorkflows
  >);
});

describe("ScheduleDialog", () => {
  it("fills the form from a template and creates an agent schedule", async () => {
    const onClose = vi.fn();
    render(<ScheduleDialog onClose={onClose} />);

    fireEvent.click(screen.getByTestId("schedule-template-morning-briefing"));
    expect(screen.getByTestId("cadence-preview")).toHaveTextContent("Every day at 07:30");

    fireEvent.click(screen.getByRole("checkbox", { name: /execute_code_sandbox/ }));
    fireEvent.click(screen.getByTestId("schedule-save"));

    await waitFor(() =>
      expect(save.mutateAsync).toHaveBeenCalledWith({
        jobId: undefined,
        payload: {
          name: "Morning briefing",
          kind: "Agent",
          cronExpression: "0 30 7 ? * *",
          agentName: "TimelinePlanner",
          workflowId: undefined,
          prompt: "Prepare my morning briefing.",
          preApprovedTools: ["execute_code_sandbox"],
        },
      }),
    );
    expect(onClose).toHaveBeenCalled();
  });

  it("clears pre-approvals when the prompt of an existing schedule changes", async () => {
    render(<ScheduleDialog job={briefing} onClose={vi.fn()} />);

    expect(screen.getByRole("checkbox", { name: /execute_code_sandbox/ })).toBeChecked();

    fireEvent.change(screen.getByLabelText("Prompt"), { target: { value: "Brief me about everything" } });

    expect(screen.getByRole("checkbox", { name: /execute_code_sandbox/ })).not.toBeChecked();
    expect(screen.getByText(/earlier pre-approvals were cleared/i)).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("schedule-save"));
    await waitFor(() =>
      expect(save.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({ jobId: "schedule-2", payload: expect.objectContaining({ preApprovedTools: [] }) }),
      ),
    );
  });

  it("only lets a script schedule's name and cadence change", () => {
    render(<ScheduleDialog job={{ ...briefing, kind: "Script", cronExpression: "0 0 * ? * *" }} onClose={vi.fn()} />);

    expect(screen.queryByLabelText("Prompt")).not.toBeInTheDocument();
    expect(screen.queryByText("Pre-approved tools")).not.toBeInTheDocument();
    expect(screen.getByText("Times are UTC.")).toBeInTheDocument();
  });

  it("shows the server's reason when saving fails", async () => {
    vi.mocked(save.mutateAsync).mockRejectedValueOnce(new Error("You have no agent named 'Gone'."));
    render(<ScheduleDialog job={briefing} onClose={vi.fn()} />);

    fireEvent.click(screen.getByTestId("schedule-save"));

    expect(await screen.findByText("You have no agent named 'Gone'.")).toBeInTheDocument();
  });
});
