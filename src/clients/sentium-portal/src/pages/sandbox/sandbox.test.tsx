import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import Sandbox from "./sandbox";
import * as execHook from "../../hooks/useSandboxExecutions";
import * as statsHook from "../../hooks/useSandboxStats";
import type { SandboxExecutionLog } from "../../types/sandbox";

const navigate = vi.fn();
vi.mock("react-router", async (orig) => ({
  ...(await orig<typeof import("react-router")>()),
  useNavigate: () => navigate,
}));

const run: SandboxExecutionLog = {
  jobId: "job-1",
  agentId: "agent-1",
  language: "Python",
  status: "Succeeded",
  executedAt: "2025-01-01T00:00:00Z",
  durationMs: 1200,
  exitCode: 0,
  artifacts: [],
  timedOut: false,
} as unknown as SandboxExecutionLog;

const baseExec = {
  executions: [] as SandboxExecutionLog[],
  totalCount: 0,
  hasMore: false,
  loadMore: vi.fn(),
  isLoadingMore: false,
  status: null,
  setStatus: vi.fn(),
  language: null,
  setLanguage: vi.fn(),
  search: "",
  setSearch: vi.fn(),
  isLoading: false,
  isFetching: false,
  error: null,
  refetch: vi.fn(),
};

const setExec = (overrides: Partial<typeof baseExec> = {}) =>
  vi.spyOn(execHook, "useSandboxExecutions").mockReturnValue({
    ...baseExec,
    ...overrides,
  } as unknown as ReturnType<typeof execHook.useSandboxExecutions>);

beforeEach(() => {
  navigate.mockReset();
  setExec();
  vi.spyOn(statsHook, "useSandboxStats").mockReturnValue({
    stats: { total: 10, succeeded: 7, failed: 2, denied: 1 },
    isLoading: false,
    error: null,
    refetch: vi.fn(),
  } as unknown as ReturnType<typeof statsHook.useSandboxStats>);
});

describe("Sandbox states", () => {
  it("renders the page title and stat values", () => {
    render(<Sandbox />);
    expect(screen.getByText("Sandbox Inspector")).toBeInTheDocument();
    expect(screen.getByText("Total Runs")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
  });

  it("shows an empty state when there are no executions", () => {
    render(<Sandbox />);
    expect(screen.getByText("No executions found")).toBeInTheDocument();
  });

  it("renders an execution row and navigates on click", () => {
    setExec({ executions: [run] });
    render(<Sandbox />);
    expect(screen.getByText("agent-1")).toBeInTheDocument();
    fireEvent.click(screen.getByText("agent-1"));
    expect(navigate).toHaveBeenCalledWith("/sandbox/job-1");
  });
});

describe("Sandbox toolbar", () => {
  it("updates the search term as the user types", () => {
    const setSearch = vi.fn();
    setExec({ setSearch });
    render(<Sandbox />);
    fireEvent.change(screen.getByPlaceholderText(/search by agent/i), { target: { value: "abc" } });
    expect(setSearch).toHaveBeenCalledWith("abc");
  });

  it("applies a status filter when a chip is clicked", () => {
    const setStatus = vi.fn();
    setExec({ setStatus });
    render(<Sandbox />);
    fireEvent.click(screen.getByRole("button", { name: "Failed" }));
    expect(setStatus).toHaveBeenCalledWith("Failed");
  });

  it("refetches when the refresh button is clicked", () => {
    const refetch = vi.fn();
    setExec({ refetch });
    render(<Sandbox />);
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });
});

describe("Sandbox pagination", () => {
  it("shows Load more and fetches the next page when clicked", () => {
    const loadMore = vi.fn();
    setExec({ executions: [run], hasMore: true, loadMore });
    render(<Sandbox />);
    const loadMoreBtn = screen.getByRole("button", { name: /load more/i });
    fireEvent.click(loadMoreBtn);
    expect(loadMore).toHaveBeenCalled();
  });
});
