import type * as d3 from "d3";

export type VisualizationMode = "constellation" | "neural" | "cluster";

export interface GraphNode extends d3.SimulationNodeDatum {
  id: string;
  content: string;
  fullContent: string;
  source: string;
  sourceType: string;
  collection: string;
  createdAt: string;
  metadata: Record<string, string>;
  radius: number;
  color: number;
  colorStr: string;
  glowColor: string;
  pulse: number;
  queryScore?: number;
  highlighted?: boolean;
  _spriteIdx?: number;
}

export interface GraphLink extends d3.SimulationLinkDatum<GraphNode> {
  strength: number;
  _id?: string;
}

export interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  life: number;
  maxLife: number;
  color: number;
  size: number;
}

export interface TraversalWave {
  originX: number;
  originY: number;
  radius: number;
  maxRadius: number;
  speed: number;
  life: number;
  color: number;
}

export interface CollectionTheme {
  color: string;
  colorHex: number;
  glow: string;
  label: string;
}

export const COLLECTION_COLORS: Record<string, CollectionTheme> = {
  knowledge_base: {
    color: "#06b6d4",
    colorHex: 0x06b6d4,
    glow: "rgba(6,182,212,0.6)",
    label: "Knowledge Base",
  },
  agent_learnings: {
    color: "#a855f7",
    colorHex: 0xa855f7,
    glow: "rgba(168,85,247,0.6)",
    label: "Agent Learnings",
  },
  user_memories: {
    color: "#f59e0b",
    colorHex: 0xf59e0b,
    glow: "rgba(245,158,11,0.6)",
    label: "User Memories",
  },
  default: {
    color: "#64748b",
    colorHex: 0x64748b,
    glow: "rgba(100,116,139,0.4)",
    label: "Other",
  },
};

export function getCollectionTheme(collection: string): CollectionTheme {
  return COLLECTION_COLORS[collection] ?? COLLECTION_COLORS.default;
}

export interface CameraState {
  x: number;
  y: number;
  zoom: number;
  targetX: number;
  targetY: number;
  targetZoom: number;
}
