import { client } from "@/shared/api/client";
import type { ToolDescriptor } from "@/features/agents/types/agents";
import type {
  BackgroundJobsSnapshot,
  CronJobRecord,
  PreApprovalTarget,
  SaveSchedulePayload,
  ScheduleTemplate,
} from "@/features/scheduler/types/scheduler";

const BASE = "/praxis/scheduler";

const jobUrl = (jobId: string) => `${BASE}/jobs/${encodeURIComponent(jobId)}`;

export const fetchActiveSchedulerJobs = (): Promise<CronJobRecord[]> => client.get<CronJobRecord[]>(`${BASE}/jobs`);

export const createSchedule = (payload: SaveSchedulePayload) => client.post<CronJobRecord>(`${BASE}/jobs`, payload);

export const updateSchedule = (jobId: string, payload: SaveSchedulePayload) =>
  client.put<CronJobRecord>(jobUrl(jobId), payload);

export const pauseSchedule = (jobId: string) => client.post<CronJobRecord>(`${jobUrl(jobId)}/pause`, null);

export const resumeSchedule = (jobId: string) => client.post<CronJobRecord>(`${jobUrl(jobId)}/resume`, null);

export const deleteSchedule = async (jobId: string): Promise<void> => {
  await client.delete(jobUrl(jobId));
};

export const fetchScheduleTemplates = () => client.get<ScheduleTemplate[]>(`${BASE}/templates`);

export const fetchPreApprovableTools = (target: PreApprovalTarget) => {
  const params = new URLSearchParams({ kind: target.kind });
  if (target.agentName) {
    params.set("agentName", target.agentName);
  }
  if (target.workflowId) {
    params.set("workflowId", target.workflowId);
  }
  if (target.jobId) {
    params.set("jobId", target.jobId);
  }
  return client.get<ToolDescriptor[]>(`${BASE}/pre-approvable-tools?${params.toString()}`);
};

export const fetchBackgroundJobs = () => client.get<BackgroundJobsSnapshot>(`${BASE}/background-jobs`);
