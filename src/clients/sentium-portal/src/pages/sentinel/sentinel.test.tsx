import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import Sentinel from "./sentinel";
import * as auditHook from "../../hooks/useSentinelAudit";
import * as settingsHook from "../../hooks/useSentinelSettings";
import * as modelsHook from "../../hooks/useOllamaModels";
import type { AuditRecord, AuditStats, PdpSettings } from "../../types/sentinel";

const record = {
  id: "r1",
  timestamp: "2025-01-01T12:00:00Z",
  agentId: "agent-1",
  skillName: "search",
  action: "Read",
  allowed: true,
  effect: "Allow",
  risk: "Low",
  alignmentVerdict: "Aligned",
  resourceType: "File",
  resourceId: "f1",
  triggeredPolicies: ["p1"],
  evaluationDurationMs: 12,
  correlationId: "corr-1",
  reason: "Within policy",
} as unknown as AuditRecord;

const stats = {
  total: 20,
  allowed: 15,
  denied: 5,
  alerts: 1,
  lowRisk: 10,
  mediumRisk: 5,
  highRisk: 3,
  criticalRisk: 2,
  latestAlignmentScore: 0.8,
} as unknown as AuditStats;

const settings = {
  autonomyLevel: 5,
  lockdownMode: false,
  semanticIntentCheckEnabled: true,
  intentCheckModel: "gemma",
  rateLimitMaxRequests: 100,
  rateLimitWindowSeconds: 60,
} as unknown as PdpSettings;

const updateSettings = vi.fn();
const refetch = vi.fn();

const setAudit = (records: AuditRecord[], isLoading = false) =>
  vi.spyOn(auditHook, "useSentinelAudit").mockReturnValue({
    records,
    isLoading,
    error: null,
    refetch,
  } as unknown as ReturnType<typeof auditHook.useSentinelAudit>);

const setSettings = (s: PdpSettings | undefined) =>
  vi.spyOn(settingsHook, "useSentinelSettings").mockReturnValue({
    settings: s,
    isLoading: false,
    isUpdating: false,
    error: null,
    updateSettings,
  } as unknown as ReturnType<typeof settingsHook.useSentinelSettings>);

beforeEach(() => {
  updateSettings.mockReset();
  refetch.mockReset();
  setAudit([record]);
  vi.spyOn(auditHook, "useSentinelStats").mockReturnValue({
    stats,
    isLoading: false,
    error: null,
  } as unknown as ReturnType<typeof auditHook.useSentinelStats>);
  setSettings(settings);
  vi.spyOn(modelsHook, "default").mockReturnValue({
    models: [{ name: "gemma" }],
  } as unknown as ReturnType<typeof modelsHook.default>);
});

describe("Sentinel states", () => {
  it("renders the title and stat values", () => {
    render(<Sentinel />);
    expect(screen.getByText("Sentinel")).toBeInTheDocument();
    expect(screen.getByText("Total Decisions")).toBeInTheDocument();
    expect(screen.getByText("25%")).toBeInTheDocument();
  });

  it("shows a loading message while audit records load", () => {
    setAudit([], true);
    render(<Sentinel />);
    expect(screen.getByText(/loading audit records/i)).toBeInTheDocument();
  });

  it("shows an empty message when there are no decisions", () => {
    setAudit([]);
    render(<Sentinel />);
    expect(screen.getByText(/no decisions recorded yet/i)).toBeInTheDocument();
  });

  it("renders an audit row and expands it to reveal the reason", () => {
    render(<Sentinel />);
    expect(screen.getByText("agent-1")).toBeInTheDocument();
    fireEvent.click(screen.getByText("agent-1"));
    expect(screen.getByText("Within policy")).toBeInTheDocument();
  });

  it("shows the lockdown banner when lockdown mode is on", () => {
    setSettings({ ...settings, lockdownMode: true });
    render(<Sentinel />);
    expect(screen.getByText(/lockdown active/i)).toBeInTheDocument();
  });
});

describe("Sentinel sovereign controls", () => {
  it("toggles lockdown mode", () => {
    render(<Sentinel />);
    fireEvent.click(screen.getByTestId("lockdown-toggle"));
    expect(updateSettings).toHaveBeenCalledWith({ lockdownMode: true });
  });

  it("toggles the semantic intent check", () => {
    render(<Sentinel />);
    fireEvent.click(screen.getByTestId("semantic-intent-toggle"));
    expect(updateSettings).toHaveBeenCalledWith({ semanticIntentCheckEnabled: false });
  });

  it("commits a new autonomy level", () => {
    render(<Sentinel />);
    const slider = screen.getByTestId("autonomy-slider");
    fireEvent.change(slider, { target: { value: "8" } });
    fireEvent.keyUp(slider);
    expect(updateSettings).toHaveBeenCalledWith({ autonomyLevel: 8 });
  });

  it("refetches audit records on refresh", () => {
    render(<Sentinel />);
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });
});
