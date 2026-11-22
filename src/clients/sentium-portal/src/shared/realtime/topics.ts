/** Mirrors the server's `RealtimeTopics`; renaming one breaks open sessions. */
export const REALTIME_TOPICS = {
  connected: "connected",
  notifications: "notifications",
  vigilStatus: "vigil.status",
  vigilIncidentOpened: "vigil.incident.opened",
  vigilIncidentResolved: "vigil.incident.resolved",
  vigilDeviceUpdated: "vigil.device.updated",
  vigilDeviceRemoved: "vigil.device.removed",
  workspaceFiles: "workspaces.files",
  workflowRuns: "workflows.runs",
  sandboxExecutions: "sandbox.executions",
  inventoryKnowledge: "inventory.knowledge",
  evaluations: "evaluations",
  schedules: "scheduler.jobs",
  sentinelAudit: "sentinel.audit",
  vault: "vault",
} as const;

export type RealtimeTopic = (typeof REALTIME_TOPICS)[keyof typeof REALTIME_TOPICS];

/**
 * How often a query that is kept live by realtime events still refetches on its own: a safety net for an event that
 * was lost, never the primary way it updates.
 */
export const REALTIME_FALLBACK_POLL_MS = 60_000;
