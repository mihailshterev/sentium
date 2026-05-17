export type PolicyEffect = "Allow" | "Deny" | "DenyWithAlert";
export type PolicyRiskLevel = "Low" | "Medium" | "High" | "Critical";
export type AlignmentVerdict = "Aligned" | "Misaligned" | "Inconclusive";

export interface AuditRecord {
  id: string;
  timestamp: string;
  agentId: string;
  skillName: string;
  resourceType: string;
  resourceId: string;
  action: string;
  userPromptHash: string;
  correlationId: string;
  metadata: Record<string, string>;
  allowed: boolean;
  effect: PolicyEffect;
  reason: string;
  risk: PolicyRiskLevel;
  triggeredPolicies: string[];
  evaluationDurationMs: number;
  alignmentVerdict: AlignmentVerdict | null;
}

export interface PdpSettings {
  lockdownMode: boolean;
  autonomyLevel: number;
  semanticIntentCheckEnabled: boolean;
  rateLimitMaxRequests: number;
  rateLimitWindowSeconds: number;
}

export interface UpdatePdpSettingsPayload {
  lockdownMode?: boolean;
  autonomyLevel?: number;
  semanticIntentCheckEnabled?: boolean;
  rateLimitMaxRequests?: number;
  rateLimitWindowSeconds?: number;
}

export interface AuditStats {
  total: number;
  allowed: number;
  denied: number;
  alerts: number;
  lowRisk: number;
  mediumRisk: number;
  highRisk: number;
  criticalRisk: number;
  latestAlignmentScore: number | null;
}
