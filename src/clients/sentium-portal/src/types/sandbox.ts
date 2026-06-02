export type SandboxLanguage = "Python" | "Node";

export type SandboxStatusFilter = "Succeeded" | "Failed" | "Denied";

export interface SandboxStats {
  total: number;
  succeeded: number;
  failed: number;
  denied: number;
}

export interface ArtifactDto {
  fileName: string;
  mimeType: string;
  blobUri: string;
  downloadPath: string;
  sizeBytes: number;
}

export interface SandboxFileContextDto {
  fileName: string;
  content: string;
}

export interface SandboxExecutionLog {
  jobId: string;
  executedAt: string;
  agentId: string;
  correlationId: string;
  language: SandboxLanguage;
  code: string;
  originalUserPrompt?: string;
  fileContext: SandboxFileContextDto[];
  succeeded: boolean;
  exitCode: number;
  output: string;
  error: string;
  timedOut: boolean;
  policyDenied: boolean;
  policyDenialReason?: string;
  sentinelAuditId: string;
  durationMs: number;
  artifacts: ArtifactDto[];
}
