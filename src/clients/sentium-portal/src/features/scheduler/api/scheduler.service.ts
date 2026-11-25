import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { ToolDescriptor } from "@/features/agents/types/agents";
import type {
  BackgroundJobsSnapshot,
  CronJobRecord,
  PreApprovalTarget,
  SaveSchedulePayload,
  ScheduleTemplate,
} from "@/features/scheduler/types/scheduler";

const BASE = "/praxis/scheduler";
const url = pathIn(BASE);

const jobUrl = (jobId: string) => url`/jobs/${jobId}`;

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

export const fetchPreApprovableTools = ({ kind, agentName, workflowId, jobId }: PreApprovalTarget) =>
  client.get<ToolDescriptor[]>(withQuery(`${BASE}/pre-approvable-tools`, { kind, agentName, workflowId, jobId }));

export const fetchBackgroundJobs = () => client.get<BackgroundJobsSnapshot>(`${BASE}/background-jobs`);
