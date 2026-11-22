import { describe, expect, it } from "vitest";
import { collectAttention, type AttentionInputs } from "./useAttention";
import type { Notification } from "@/features/notifications/types/notification";
import type { SystemMetrics } from "@/features/system/types/system";
import type { ServiceHealthStatus } from "@/features/vigil/types/serviceHealth";
import type { WorkflowRun } from "@/features/workflows/types/workflows";

const none: AttentionInputs = { services: [], incidents: [], metrics: null, stats: null, runs: [], notifications: [] };

const service = (serviceName: string, status: ServiceHealthStatus["status"]): ServiceHealthStatus => ({
  serviceName,
  kind: "Service",
  status,
  latencyMs: 3,
  checkedAt: "2026-11-22T10:00:00Z",
  checks: [],
  uptimePercent: 100,
  lastStateChange: "2026-11-22T09:00:00Z",
  consecutiveFailures: 0,
});

const metrics = (memoryLoadPercent: number, diskUsage = 40): SystemMetrics =>
  ({
    memory: { totalMb: 32768, usedMb: 0, availableMb: 1024, memoryLoadPercent },
    disks: [{ name: "C:", label: "System", usagePercent: diskUsage, availableGb: 12 }],
  }) as unknown as SystemMetrics;

const run = (id: string, risk: string, startedAt: string): WorkflowRun => ({
  id,
  title: `Run ${id}`,
  triggerType: "manual.execute",
  triggerPayload: "{}",
  explanation: "",
  risk,
  recommendation: "",
  startedAt,
  completedAt: startedAt,
});

const notification = (id: string, severity: Notification["severity"], readAt: string | null = null): Notification => ({
  id,
  category: "Backup",
  severity,
  title: `Notification ${id}`,
  body: null,
  link: null,
  occurrences: 1,
  createdAt: "2026-11-22T08:00:00Z",
  updatedAt: "2026-11-22T08:00:00Z",
  readAt,
});

describe("collectAttention", () => {
  it("is empty when nothing is wrong", () => {
    expect(
      collectAttention({ ...none, services: [service("codex", "Healthy")], metrics: metrics(40), runs: [run("1", "Low", "2026-11-22T00:00:00Z")] }),
    ).toEqual([]);
  });

  it("flags degraded and unhealthy services, not unknown ones", () => {
    const items = collectAttention({
      ...none,
      services: [service("codex", "Degraded"), service("praxis", "Unhealthy"), service("locus", "Unknown")],
    });
    expect(items.map((i) => [i.title, i.tone])).toEqual([
      ["praxis is unhealthy", "critical"],
      ["codex is degraded", "warning"],
    ]);
  });

  it("flags host memory and disks at 90% or more", () => {
    const items = collectAttention({ ...none, metrics: metrics(96, 93) });
    expect(items.map((i) => i.title)).toEqual(["Host memory at 96%", "Disk System at 93%"]);
    expect(items.every((i) => i.to === "/system")).toBe(true);
  });

  it("flags high-risk runs and links to them", () => {
    const items = collectAttention({
      ...none,
      runs: [run("1", "Low", "2026-11-22T00:00:00Z"), run("2", "High", "2026-11-22T01:00:00Z")],
    });
    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({ tone: "warning", to: "/orchestration/runs/2" });
  });

  it("surfaces only unread warning and critical notifications", () => {
    const items = collectAttention({
      ...none,
      notifications: [notification("a", "Info"), notification("b", "Warning"), notification("c", "Critical", "2026-11-22T09:00:00Z")],
    });
    expect(items.map((i) => i.id)).toEqual(["notification:b"]);
    expect(items[0].to).toBe("/inbox");
  });

  it("puts critical items first, then the newest", () => {
    const items = collectAttention({
      ...none,
      runs: [run("old", "High", "2026-11-20T00:00:00Z"), run("new", "High", "2026-11-22T00:00:00Z")],
      services: [service("praxis", "Unhealthy")],
    });
    expect(items.map((i) => i.id)).toEqual(["service:praxis", "run:new", "run:old"]);
  });

  it("flags high-risk tool calls as critical when any were critical", () => {
    const stats = { total: 10, allowed: 8, denied: 2, alerts: 0, lowRisk: 7, mediumRisk: 1, highRisk: 1, criticalRisk: 1, latestAlignmentScore: null };
    expect(collectAttention({ ...none, stats })[0]).toMatchObject({ tone: "critical", title: "2 high-risk tool calls" });
  });
});
