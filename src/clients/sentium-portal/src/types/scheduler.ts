export interface CronJobRecord {
  jobId: string;
  agentId: string;
  jobName: string;
  language: string;
  cronExpression: string;
  previousRun: string | null;
  nextRun: string | null;
  status: string;
  codeSnippet?: string;
}
