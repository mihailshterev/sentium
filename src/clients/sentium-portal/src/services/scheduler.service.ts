import { client } from "../api/client";
import type { CronJobRecord } from "../types/scheduler";

const BASE = "/agent-runtime";

export const fetchActiveSchedulerJobs = async (): Promise<CronJobRecord[]> => {
  const response = await client.get<CronJobRecord[]>(`${BASE}/scheduler/jobs`);
  return response;
};

export const deleteScheduledJob = async (agentId: string, jobId: string): Promise<void> => {
  await client.delete(`${BASE}/scheduler/agents/${encodeURIComponent(agentId)}/jobs/${encodeURIComponent(jobId)}`);
};
