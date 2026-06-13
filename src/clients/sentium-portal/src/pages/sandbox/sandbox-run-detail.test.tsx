import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import SandboxRunDetail from "./sandbox-run-detail";
import * as execHook from "../../hooks/useSandboxExecution";
import type { SandboxExecutionLog } from "../../types/sandbox";

const navigate = vi.fn();
vi.mock("react-router", async (orig) => ({
  ...(await orig<typeof import("react-router")>()),
  useNavigate: () => navigate,
  useParams: () => ({ jobId: "job-12345678" }),
}));

const execution = {
  jobId: "job-12345678",
  agentId: "agent-1",
  language: "Python",
  executedAt: "2025-01-01T00:00:00Z",
  correlationId: "corr-1234",
  sentinelAuditId: "audit-1234",
  durationMs: 1500,
  exitCode: 0,
  succeeded: true,
  policyDenied: false,
  timedOut: false,
  output: "hello world",
  error: "",
  code: "print('hi')",
  fileContext: [],
  artifacts: [
    {
      fileName: "out/result.png",
      mimeType: "image/png",
      sizeBytes: 2048,
      downloadPath: "job/result.png",
      blobUri: "blob://1",
    },
  ],
  originalUserPrompt: "Make a chart",
  policyDenialReason: null,
} as unknown as SandboxExecutionLog;

const refetch = vi.fn();

const setExecution = (exec: SandboxExecutionLog | null, isLoading = false) =>
  vi.spyOn(execHook, "useSandboxExecution").mockReturnValue({
    execution: exec,
    isLoading,
    isFetching: false,
    error: null,
    refetch,
  } as unknown as ReturnType<typeof execHook.useSandboxExecution>);

beforeEach(() => {
  navigate.mockReset();
  refetch.mockReset();
  setExecution(execution);
});

describe("SandboxRunDetail states", () => {
  it("shows a loading header while fetching", () => {
    setExecution(null, true);
    render(<SandboxRunDetail />);
    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("shows a not-found state when the run is missing", () => {
    setExecution(null);
    render(<SandboxRunDetail />);
    expect(screen.getByText("Run not found")).toBeInTheDocument();
  });

  it("renders the run header, metadata badges and summary tab by default", () => {
    render(<SandboxRunDetail />);
    expect(screen.getAllByText("agent-1").length).toBeGreaterThan(0);
    expect(screen.getByText(/exit 0/)).toBeInTheDocument();
    expect(screen.getByText("Agent ID")).toBeInTheDocument(); // summary tab content
  });
});

describe("SandboxRunDetail tabs", () => {
  it("shows the source code on the Script tab", () => {
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByRole("tab", { name: /script/i }));
    expect(screen.getByText("print('hi')")).toBeInTheDocument();
  });

  it("shows terminal output on the Output tab", () => {
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByRole("tab", { name: /output/i }));
    expect(screen.getByText("hello world")).toBeInTheDocument();
  });

  it("shows artifacts on the Artifacts tab", () => {
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByRole("tab", { name: /artifacts/i }));
    expect(screen.getByText("result.png")).toBeInTheDocument();
  });

  it("shows an empty artifacts message when there are none", () => {
    setExecution({ ...execution, artifacts: [] } as SandboxExecutionLog);
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByRole("tab", { name: /artifacts/i }));
    expect(screen.getByText(/no artifacts produced/i)).toBeInTheDocument();
  });
});

describe("SandboxRunDetail actions", () => {
  it("navigates back to the runs list", () => {
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByTitle("Back to runs"));
    expect(navigate).toHaveBeenCalledWith("/sandbox");
  });

  it("refetches on refresh", () => {
    render(<SandboxRunDetail />);
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });
});
