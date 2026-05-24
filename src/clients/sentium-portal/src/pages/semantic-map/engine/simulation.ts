import * as d3 from "d3";
import type { GraphNode, GraphLink, VisualizationMode } from "./types";
import { getCollectionTheme } from "./types";
import type { KnowledgeMapNode } from "../../../types/knowledge-map";

export function buildGraphData(nodes: KnowledgeMapNode[]): { nodes: GraphNode[]; links: GraphLink[] } {
  const gNodes: GraphNode[] = nodes.map((n) => {
    const theme = getCollectionTheme(n.collection);
    return {
      ...n,
      radius: 5,
      color: theme.colorHex,
      colorStr: theme.color,
      glowColor: theme.glow,
      pulse: Math.random() * Math.PI * 2,
      highlighted: false,
      queryScore: undefined,
    };
  });

  const sourceMap = new Map<string, GraphNode[]>();
  for (const n of gNodes) {
    const list = sourceMap.get(n.source) ?? [];
    list.push(n);
    sourceMap.set(n.source, list);
  }

  const links: GraphLink[] = [];
  let linkId = 0;
  for (const group of sourceMap.values()) {
    for (let i = 0; i < group.length - 1; i++) {
      links.push({ source: group[i], target: group[i + 1], strength: 0.6, _id: `l${linkId++}` });
      if (i + 2 < group.length) {
        links.push({ source: group[i], target: group[i + 2], strength: 0.2, _id: `l${linkId++}` });
      }
    }
  }

  const colGroups = new Map<string, GraphNode[]>();
  for (const n of gNodes) {
    const list = colGroups.get(n.collection) ?? [];
    list.push(n);
    colGroups.set(n.collection, list);
  }
  const colArr = [...colGroups.values()];
  for (let ci = 0; ci < colArr.length - 1; ci++) {
    const a = colArr[ci];
    const b = colArr[ci + 1];
    const count = Math.min(3, a.length, b.length);
    for (let k = 0; k < count; k++) {
      links.push({
        source: a[k % a.length],
        target: b[k % b.length],
        strength: 0.08,
        _id: `l${linkId++}`,
      });
    }
  }

  const degree = new Map<string, number>();
  for (const link of links) {
    const s = link.source as GraphNode;
    const t = link.target as GraphNode;
    degree.set(s.id, (degree.get(s.id) ?? 0) + 1);
    degree.set(t.id, (degree.get(t.id) ?? 0) + 1);
  }
  const maxDeg = Math.max(...degree.values(), 1);
  for (const n of gNodes) {
    const deg = degree.get(n.id) ?? 0;
    n.radius = 4 + Math.sqrt(deg / maxDeg) * 7;
  }

  return { nodes: gNodes, links };
}

export function createSimulation(
  nodes: GraphNode[],
  links: GraphLink[],
  width: number,
  height: number,
  mode: VisualizationMode,
): d3.Simulation<GraphNode, GraphLink> {
  const clusterCentres: Record<string, { x: number; y: number }> = {
    knowledge_base: { x: width * 0.35, y: height * 0.4 },
    agent_learnings: { x: width * 0.65, y: height * 0.4 },
    user_memories: { x: width * 0.5, y: height * 0.7 },
  };

  const chargeStrength = mode === "cluster" ? -120 : mode === "neural" ? -60 : -80;
  const clusterPull = mode === "cluster" ? 0.04 : mode === "neural" ? 0.01 : 0.02;
  const linkDist = mode === "neural" ? 30 : mode === "cluster" ? 55 : 40;

  const sim = d3
    .forceSimulation<GraphNode>(nodes)
    .force(
      "link",
      d3
        .forceLink<GraphNode, GraphLink>(links)
        .id((d) => d.id)
        .strength((l) => l.strength)
        .distance(linkDist),
    )
    .force("charge", d3.forceManyBody<GraphNode>().strength(chargeStrength).distanceMax(250))
    .force(
      "collide",
      d3.forceCollide<GraphNode>((d) => d.radius + 4),
    )
    .force("center", d3.forceCenter(width / 2, height / 2).strength(0.03))
    .force("cluster", () => {
      const alpha = sim.alpha();
      for (const n of nodes) {
        const c = clusterCentres[n.collection];
        if (!c) continue;
        n.vx = (n.vx ?? 0) + (c.x - (n.x ?? 0)) * clusterPull * alpha;
        n.vy = (n.vy ?? 0) + (c.y - (n.y ?? 0)) * clusterPull * alpha;
      }
    })
    .alphaDecay(0.015)
    .velocityDecay(0.3);

  return sim;
}
