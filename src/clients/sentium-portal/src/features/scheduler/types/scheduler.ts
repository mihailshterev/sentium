export type ScheduleKind = "Script" | "Agent" | "Workflow";

export type ScheduleRunStatus = "Completed" | "Failed" | "TimedOut" | "Interrupted" | "Cancelled";

export interface ScheduleLastRun {
  runId: string | null;
  status: ScheduleRunStatus | string;
  at: string;
}

export interface CronJobRecord {
  jobId: string;
  /** For a script schedule the agent that created it; for an agent schedule the agent it runs. */
  agentId: string;
  jobName: string;
  language: string;
  cronExpression: string;
  previousRun: string | null;
  nextRun: string | null;
  status: string;
  kind: ScheduleKind;
  workflowId: string | null;
  /** The agent's or workflow's name when the schedule was saved. */
  targetName: string;
  prompt: string;
  preApprovedTools: string[];
  /** The zone `cronExpression` is read in. */
  timeZone: string;
  paused: boolean;
  lastRun: ScheduleLastRun | null;
  codeSnippet?: string;
}

export interface SaveSchedulePayload {
  name: string;
  kind: ScheduleKind;
  cronExpression: string;
  agentName?: string;
  workflowId?: string;
  prompt?: string;
  /** On an edit, `null` keeps the current ones unless the agent, workflow or prompt changed. */
  preApprovedTools?: string[] | null;
}

export interface ScheduleTemplate {
  id: string;
  name: string;
  description: string;
  agentName: string;
  cronExpression: string;
  prompt: string;
}

export interface PreApprovalTarget {
  kind: Exclude<ScheduleKind, "Script">;
  agentName?: string;
  workflowId?: string;
  /** Set when editing, so the tools are those of the schedule's owner. */
  jobId?: string;
}

export type BackgroundServiceState = "NotStarted" | "Running" | "Stopped" | "Faulted" | "Hosted";

export interface BackgroundServiceInfo {
  name: string;
  type: string;
  module: string;
  state: BackgroundServiceState;
  error: string | null;
}

export interface BackgroundActivity {
  source: string;
  name: string;
  detail: string | null;
  ownerId: string | null;
  startedAt: string | null;
}

export interface BackgroundJobsSnapshot {
  services: BackgroundServiceInfo[];
  running: BackgroundActivity[];
}
