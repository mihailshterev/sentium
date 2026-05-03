export interface WorkflowRun {
  id: string;
  triggerType: string;
  triggerPayload: string;
  explanation: string;
  risk: string;
  recommendation: string;
  startedAt: string;
  completedAt: string;
}
