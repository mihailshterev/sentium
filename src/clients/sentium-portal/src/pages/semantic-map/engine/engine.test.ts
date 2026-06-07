import { describe, it, expect } from "vitest";
import { buildGraphData, createSimulation } from "./simulation";
import { DEMO_NODES } from "./demo-data";
import {
  spawnClickParticles,
  spawnSearchParticles,
  spawnTrailParticles,
  spawnTraversalWave,
  spawnSearchWaves,
} from "./particles";
import { getCollectionTheme, COLLECTION_COLORS } from "./types";
import type { GraphNode, Particle } from "./types";
import type { KnowledgeMapNode } from "../../../types/knowledge-map";

const makeNode = (overrides: Partial<KnowledgeMapNode> = {}): KnowledgeMapNode => ({
  id: "n1",
  content: "content",
  fullContent: "full content",
  source: "src",
  sourceType: "Custom",
  collection: "knowledge_base",
  createdAt: "2025-01-01T00:00:00Z",
  metadata: {},
  ...overrides,
});

const makeGraphNode = (overrides: Partial<GraphNode> = {}): GraphNode =>
  ({ ...buildGraphData([makeNode()]).nodes[0], x: 10, y: 20, ...overrides }) as GraphNode;

describe("getCollectionTheme", () => {
  it("returns the matching theme for a known collection", () => {
    expect(getCollectionTheme("agent_learnings")).toBe(COLLECTION_COLORS.agent_learnings);
  });

  it("falls back to the default theme for an unknown collection", () => {
    expect(getCollectionTheme("totally_unknown")).toBe(COLLECTION_COLORS.default);
  });
});

describe("buildGraphData", () => {
  it("returns an empty graph for empty input", () => {
    const { nodes, links } = buildGraphData([]);
    expect(nodes).toHaveLength(0);
    expect(links).toHaveLength(0);
  });

  it("maps each input node to a graph node preserving identity and adding visual fields", () => {
    const { nodes } = buildGraphData([makeNode({ id: "a", collection: "user_memories" })]);
    expect(nodes).toHaveLength(1);
    expect(nodes[0].id).toBe("a");
    expect(nodes[0].collection).toBe("user_memories");
    expect(nodes[0].color).toBe(COLLECTION_COLORS.user_memories.colorHex);
    expect(nodes[0].radius).toBeGreaterThanOrEqual(4);
  });

  it("links consecutive nodes that share a source", () => {
    const input = [
      makeNode({ id: "a", source: "s" }),
      makeNode({ id: "b", source: "s" }),
      makeNode({ id: "c", source: "s" }),
    ];
    const { links } = buildGraphData(input);
    expect(links.length).toBeGreaterThanOrEqual(3);
  });

  it("adds cross-collection links when multiple collections are present", () => {
    const input = [
      makeNode({ id: "a", collection: "knowledge_base", source: "s1" }),
      makeNode({ id: "b", collection: "agent_learnings", source: "s2" }),
    ];
    const { links } = buildGraphData(input);
    expect(links.length).toBeGreaterThan(0);
  });

  it("scales node radius with connection degree", () => {
    const { nodes } = buildGraphData(DEMO_NODES);
    expect(nodes).toHaveLength(DEMO_NODES.length);
    expect(nodes.every((n) => n.radius >= 4)).toBe(true);
  });
});

describe("createSimulation", () => {
  it.each(["constellation", "neural", "cluster"] as const)("builds a runnable simulation for %s mode", (mode) => {
    const { nodes, links } = buildGraphData(DEMO_NODES.slice(0, 9));
    const sim = createSimulation(nodes, links, 800, 600, mode);
    try {
      expect(sim.nodes()).toHaveLength(nodes.length);
      expect(sim.force("charge")).toBeTruthy();
    } finally {
      sim.stop();
    }
  });
});

describe("DEMO_NODES", () => {
  it("provides 60 demo nodes with unique ids", () => {
    expect(DEMO_NODES).toHaveLength(60);
    expect(new Set(DEMO_NODES.map((n) => n.id)).size).toBe(60);
  });

  it("only uses the three known collections", () => {
    const collections = new Set(DEMO_NODES.map((n) => n.collection));
    expect([...collections].sort()).toEqual(["agent_learnings", "knowledge_base", "user_memories"]);
  });

  it("uses valid ISO timestamps", () => {
    expect(DEMO_NODES.every((n) => !Number.isNaN(Date.parse(n.createdAt)))).toBe(true);
  });
});

describe("particle spawning", () => {
  it("spawnClickParticles adds a burst originating at the node", () => {
    const particles: Particle[] = [];
    spawnClickParticles(makeGraphNode({ x: 10, y: 20 }), particles);
    expect(particles).toHaveLength(28);
    expect(particles[0].x).toBe(10);
    expect(particles[0].y).toBe(20);
  });

  it("spawnSearchParticles adds 50 white particles", () => {
    const particles: Particle[] = [];
    spawnSearchParticles(makeGraphNode(), particles);
    expect(particles).toHaveLength(50);
    expect(particles.every((p) => p.color === 0xffffff)).toBe(true);
  });

  it("spawnTrailParticles respects the requested count", () => {
    const particles: Particle[] = [];
    spawnTrailParticles(0, 0, 0x123456, particles, 7);
    expect(particles).toHaveLength(7);
  });

  it("spawnTraversalWave originates at the node with the given max radius", () => {
    const wave = spawnTraversalWave(makeGraphNode({ x: 5, y: 6 }), 400);
    expect(wave.originX).toBe(5);
    expect(wave.originY).toBe(6);
    expect(wave.maxRadius).toBe(400);
  });

  it("spawnSearchWaves returns three layered waves", () => {
    const waves = spawnSearchWaves(makeGraphNode());
    expect(waves).toHaveLength(3);
    expect(waves.map((w) => w.maxRadius)).toEqual([300, 500, 700]);
  });
});
