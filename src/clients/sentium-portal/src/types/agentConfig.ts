export interface SystemSettings {
  userHarnessPrompt: string;
  isBuiltInHarnessEnabled: boolean;
  updatedAt: string;
  updatedBy: string | null;
}

export interface UpdateSystemSettingsPayload {
  userHarnessPrompt: string;
  isBuiltInHarnessEnabled: boolean;
}

export interface AgentLearning {
  id: string;
  agentName: string;
  content: string;
  tags: string;
  conversationId: string | null;
  capturedAt: string;
  isIngested: boolean;
}

export interface AgentLearningStats {
  totalLearnings: number;
  pendingIngestion: number;
  learningsByAgent: Record<string, number>;
}

export interface CaptureAgentLearningPayload {
  agentName: string;
  content: string;
  tags?: string;
  conversationId?: string;
}

export interface KnowledgeBaseCollectionStats {
  collectionName: string;
  pointCount: number;
  vectorSize: number;
  distanceMetric: string;
}
