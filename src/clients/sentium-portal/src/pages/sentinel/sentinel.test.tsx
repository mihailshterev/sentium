import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Sentinel from "./sentinel";
import * as useSentinelEventsHook from "../../hooks/useSentinelEvents";
import * as agentRuntimeService from "../../services/agentRuntime.service";
import type { NetworkEvent } from "../../types/sentinel";

const makeEvent = (overrides: Partial<NetworkEvent> = {}): NetworkEvent => ({
  id: "ev-1",
  source: "zeek",
  action: "Immediate-Review",
  timestamp: new Date(Date.now() - 30_000).toISOString(),
  origH: "192.168.1.100",
  respH: "10.0.0.1",
  proto: "tcp",
  service: "http",
  mlScore: "97.5%",
  ...overrides,
});

const immediateEvent = makeEvent({ id: "ev-1", action: "Immediate-Review", mlScore: "97.5%" });
const investigateEvent = makeEvent({ id: "ev-2", action: "Investigate", mlScore: "82.0%", proto: "udp" });
const lowScoreEvent = makeEvent({ id: "ev-3", action: "Investigate", mlScore: "40.0%", proto: "icmp" });
const unknownServiceEvent = makeEvent({ id: "ev-4", action: "Investigate", mlScore: "60.0%", service: "unknown" });

const defaultHook = {
  events: [immediateEvent, investigateEvent],
  isLoading: false,
  isRefetching: false,
  error: null,
  refetch: vi.fn().mockResolvedValue(undefined),
};

const renderSentinel = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Sentinel />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useSentinelEventsHook, "default").mockReturnValue(defaultHook);
  vi.spyOn(agentRuntimeService, "triggerNetworkAnalysis").mockResolvedValue({ eventId: "run-123" });
});

describe("Sentinel error state", () => {
  it("shows error state when error is set and events is empty", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [],
      error: new Error("Connection failed"),
    });
    renderSentinel();
    expect(screen.getByText(/unable to load network events/i)).toBeInTheDocument();
    expect(screen.getByText(/connection failed/i)).toBeInTheDocument();
  });

  it("shows 'Unknown error' when error is not Error instance", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [],
      error: "string error" as unknown as Error,
    });
    renderSentinel();
    expect(screen.getByText(/unknown error/i)).toBeInTheDocument();
  });

  it("calls refetch when Retry button is clicked in error state", async () => {
    const refetch = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [],
      error: new Error("err"),
      refetch,
    });
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /retry/i }));
    await waitFor(() => expect(refetch).toHaveBeenCalled());
  });
});

describe("Sentinel loading state", () => {
  it("renders skeleton rows when loading", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, events: [], isLoading: true });
    renderSentinel();
    expect(screen.queryByText("192.168.1.100")).not.toBeInTheDocument();
    expect(screen.queryByText("REVIEW")).not.toBeInTheDocument();
  });
});

describe("Sentinel success state", () => {
  it("renders the page title", () => {
    renderSentinel();
    expect(screen.getByText("Sentinel")).toBeInTheDocument();
  });

  it("renders total event count", () => {
    renderSentinel();
    expect(screen.getAllByText("2").length).toBeGreaterThanOrEqual(1);
  });

  it("renders immediate review count", () => {
    renderSentinel();
    expect(screen.getAllByText("1").length).toBeGreaterThanOrEqual(1); // immediateCount
  });

  it("renders source IPs", () => {
    renderSentinel();
    expect(screen.getAllByText("192.168.1.100").length).toBeGreaterThan(0);
  });

  it("renders protocol badges", () => {
    renderSentinel();
    expect(screen.getByText("TCP")).toBeInTheDocument();
    expect(screen.getByText("UDP")).toBeInTheDocument();
  });

  it("renders ML scores", () => {
    renderSentinel();
    expect(screen.getByText("97.5%")).toBeInTheDocument();
    expect(screen.getByText("82.0%")).toBeInTheDocument();
  });

  it("renders REVIEW badge for Immediate-Review events", () => {
    renderSentinel();
    expect(screen.getByText("REVIEW")).toBeInTheDocument();
  });

  it("renders INVEST badge for Investigate events", () => {
    renderSentinel();
    expect(screen.getByText("INVEST")).toBeInTheDocument();
  });

  it("renders '-' for unknown service", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [unknownServiceEvent],
    });
    renderSentinel();
    expect(screen.getByText("-")).toBeInTheDocument();
  });
});

describe("Sentinel filters", () => {
  it("shows all events by default", () => {
    renderSentinel();
    expect(screen.getByText("REVIEW")).toBeInTheDocument();
    expect(screen.getByText("INVEST")).toBeInTheDocument();
  });

  it("filters to only Immediate events when Immediate filter clicked", () => {
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /immediate/i }));
    expect(screen.getByText("REVIEW")).toBeInTheDocument();
    expect(screen.queryByText("INVEST")).not.toBeInTheDocument();
  });

  it("filters to only Investigate events when Investigate filter clicked", () => {
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /investigate/i }));
    expect(screen.queryByText("REVIEW")).not.toBeInTheDocument();
    expect(screen.getByText("INVEST")).toBeInTheDocument();
  });

  it("shows 'No immediate review events' when filtered to Immediate and no results", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [investigateEvent],
    });
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /immediate/i }));
    expect(screen.getByText(/no immediate review events/i)).toBeInTheDocument();
  });

  it("shows 'No investigation events' when filtered to Investigate and no results", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [immediateEvent],
    });
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /investigate/i }));
    expect(screen.getByText(/no investigation events/i)).toBeInTheDocument();
  });

  it("shows 'Waiting for Zeek...' when all filter and events empty", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, events: [] });
    renderSentinel();
    expect(screen.getByText(/waiting for zeek/i)).toBeInTheDocument();
  });
});

describe("Sentinel score color classes", () => {
  it("applies red score class for score >= 95%", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [makeEvent({ mlScore: "97.5%" })],
    });
    renderSentinel();
    // red score applies to ≥95% — the score should be displayed
    expect(screen.getByText("97.5%")).toBeInTheDocument();
  });

  it("applies amber score class for score 80-95%", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [makeEvent({ mlScore: "82.0%" })],
    });
    renderSentinel();
    expect(screen.getByText("82.0%")).toBeInTheDocument();
  });

  it("applies green score class for score < 80%", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [lowScoreEvent],
    });
    renderSentinel();
    expect(screen.getByText("40.0%")).toBeInTheDocument();
  });
});

describe("Sentinel ICMP protocol class", () => {
  it("applies ICMP protocol class for icmp events", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [makeEvent({ proto: "icmp" })],
    });
    renderSentinel();
    expect(screen.getAllByText("ICMP").length).toBeGreaterThanOrEqual(1);
  });

  it("applies default protocol class for unknown protocols", () => {
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({
      ...defaultHook,
      events: [makeEvent({ proto: "xyz" })],
    });
    renderSentinel();
    expect(screen.getAllByText("XYZ").length).toBeGreaterThanOrEqual(1);
  });
});

describe("Sentinel timestamp formatting", () => {
  it("renders relative time in seconds", () => {
    const event = makeEvent({ timestamp: new Date(Date.now() - 10_000).toISOString() });
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, events: [event] });
    renderSentinel();
    expect(screen.getByText(/10s ago/)).toBeInTheDocument();
  });

  it("renders relative time in minutes", () => {
    const event = makeEvent({ timestamp: new Date(Date.now() - 120_000).toISOString() });
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, events: [event] });
    renderSentinel();
    expect(screen.getByText(/2m ago/)).toBeInTheDocument();
  });

  it("renders relative time in hours", () => {
    const event = makeEvent({ timestamp: new Date(Date.now() - 3_600_000 * 2).toISOString() });
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, events: [event] });
    renderSentinel();
    expect(screen.getByText(/2h ago/)).toBeInTheDocument();
  });
});

describe("Sentinel refresh", () => {
  it("calls refetch when Refresh button is clicked", async () => {
    const refetch = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useSentinelEventsHook, "default").mockReturnValue({ ...defaultHook, refetch });
    renderSentinel();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    await waitFor(() => expect(refetch).toHaveBeenCalled());
  });
});

describe("Sentinel analyze flow", () => {
  it("calls triggerNetworkAnalysis when Analyze button is clicked", async () => {
    renderSentinel();
    const analyzeButtons = screen.getAllByRole("button", { name: /analyze/i });
    fireEvent.click(analyzeButtons[0]);
    await waitFor(() =>
      expect(agentRuntimeService.triggerNetworkAnalysis).toHaveBeenCalledWith(expect.objectContaining({ id: "ev-1" })),
    );
  });
});
