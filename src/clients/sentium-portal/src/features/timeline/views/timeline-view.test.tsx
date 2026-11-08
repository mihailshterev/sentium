import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import * as hooks from "../hooks/useTimeline";
import { monthDetail, proposal, task, yearOverview } from "../test-fixtures";
import { TimelineView } from "./timeline-view";

vi.mock("../hooks/useTimeline", () => ({
  useYearOverview: vi.fn(),
  useMonthDetail: vi.fn(),
  usePendingProposals: vi.fn(),
  useTaskMutations: vi.fn(),
  useNoteMutations: vi.fn(),
  useProposalMutations: vi.fn(),
  timelineKeys: { all: ["timeline"] },
}));

vi.mock("@/features/agents/hooks/useAgentRoster", () => ({
  useAgentRoster: () => ({ roster: [], isLoading: false, isError: false }),
}));

vi.mock("../components/agent-chat-drawer", () => ({
  AgentChatDrawer: ({ context, onClose }: { context: { kind: string }; onClose: () => void }) => (
    <aside aria-label="Agent chat">
      chat about {context.kind}
      <button onClick={onClose}>close chat</button>
    </aside>
  ),
}));

const mutation = () => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, error: null, variables: undefined });

function CurrentUrl() {
  const location = useLocation();
  return <output data-testid="url">{location.pathname + location.search}</output>;
}

const renderAt = (url: string) =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/timeline" element={<TimelineView />} />
      </Routes>
      <CurrentUrl />
    </MemoryRouter>,
  );

describe("TimelineView", () => {
  const taskMutations = {
    create: mutation(),
    update: mutation(),
    remove: mutation(),
    schedule: mutation(),
    setStatus: mutation(),
  };
  const proposalMutations = { accept: mutation(), reject: mutation() };

  beforeEach(() => {
    vi.mocked(hooks.useYearOverview).mockReturnValue({
      overview: yearOverview(2026, { 11: { taskCount: 1, openCount: 1 } }),
      isLoading: false,
      isError: false,
    });
    vi.mocked(hooks.useMonthDetail).mockReturnValue({
      detail: monthDetail({ tasks: [task()], proposals: [proposal()] }),
      isLoading: false,
      isError: false,
    });
    vi.mocked(hooks.usePendingProposals).mockReturnValue({ proposals: [proposal()], isLoading: false });
    vi.mocked(hooks.useTaskMutations).mockReturnValue(taskMutations as never);
    vi.mocked(hooks.useNoteMutations).mockReturnValue({ create: mutation(), remove: mutation() } as never);
    vi.mocked(hooks.useProposalMutations).mockReturnValue(proposalMutations as never);
  });

  it("shows the year and opens a month in the URL", async () => {
    renderAt("/timeline?year=2026");

    expect(screen.getByRole("heading", { level: 1, name: "Timeline" })).toBeInTheDocument();
    expect(screen.getByText("Select a month to open its calendar.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /^November 2026/ }));

    expect(screen.getByTestId("url")).toHaveTextContent("/timeline?year=2026&month=11");
    expect(await screen.findByRole("heading", { level: 2, name: "November 2026" })).toBeInTheDocument();
  });

  it("collapses the month with Escape or its close button", () => {
    renderAt("/timeline?year=2026&month=11");
    expect(screen.getByTestId("month-calendar")).toBeInTheDocument();

    fireEvent.keyDown(window, { key: "Escape" });

    expect(screen.getByTestId("url")).toHaveTextContent("/timeline?year=2026");
    expect(screen.getByTestId("url")).not.toHaveTextContent("month");
  });

  it("switches years", () => {
    renderAt("/timeline?year=2026&month=4");

    fireEvent.click(screen.getByRole("button", { name: "Next year" }));

    expect(screen.getByTestId("url")).toHaveTextContent("/timeline?year=2027");
  });

  it("creates a task from a day", () => {
    renderAt("/timeline?year=2026&month=11");

    fireEvent.click(screen.getByRole("button", { name: "Add task on 20 November" }));

    expect(screen.getByRole("dialog", { name: "New task" })).toBeInTheDocument();
    expect(screen.getByLabelText("Day")).toHaveValue("2026-11-20");
  });

  it("answers agent suggestions and opens the chat drawer", async () => {
    renderAt("/timeline?year=2026&month=11");

    fireEvent.click(screen.getAllByRole("button", { name: 'Reject suggestion: Add "Plan winter trip"' })[0]);
    expect(proposalMutations.reject.mutate).toHaveBeenCalledWith(
      expect.objectContaining({ id: "proposal-1" }),
      expect.anything(),
    );

    fireEvent.click(screen.getByRole("button", { name: "Plan with agent" }));
    expect(screen.getByRole("complementary", { name: "Agent chat" })).toHaveTextContent("chat about month");

    // The first Escape closes the drawer (it animates out); the month stays open.
    fireEvent.keyDown(window, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("complementary", { name: "Agent chat" })).not.toBeInTheDocument());
    expect(screen.getByTestId("month-calendar")).toBeInTheDocument();
  });

  it("lists every pending suggestion", () => {
    renderAt("/timeline?year=2026");

    fireEvent.click(screen.getByTestId("timeline-suggestions"));

    expect(screen.getByRole("dialog", { name: "Agent suggestions" })).toBeInTheDocument();
    expect(screen.getByTestId("proposal-proposal-1")).toBeInTheDocument();
  });
});
