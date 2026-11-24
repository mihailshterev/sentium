export interface AgentLearning {
  id: string;
  agentName: string;
  content: string;
  tags: string;
  conversationId: string | null;
  capturedAt: string;
  isIngested: boolean;
  isGlobal: boolean;
  /** How often recall has handed this learning to an agent. */
  recallCount?: number;
  lastRecalledAt?: string | null;
}

export interface AgentLearningStats {
  totalLearnings: number;
  pendingIngestion: number;
  globalLearnings: number;
  learningsByAgent: Record<string, number>;
}

export interface KnowledgeBaseCollectionStats {
  collectionName: string;
  pointCount: number;
  vectorSize: number;
  distanceMetric: string;
}
