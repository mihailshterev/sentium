export interface HarnessSettings {
  userHarnessPrompt: string;
  isBuiltInHarnessEnabled: boolean;
}

export interface Settings {
  harness: HarnessSettings;
  updatedAt: string;
  updatedBy: string | null;
}

export interface UpdateSettingsPayload {
  harness: HarnessSettings;
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

export interface KnowledgeBaseCollectionStats {
  collectionName: string;
  pointCount: number;
  vectorSize: number;
  distanceMetric: string;
}
