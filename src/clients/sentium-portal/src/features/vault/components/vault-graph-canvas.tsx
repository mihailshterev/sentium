import { useEffect, useRef, useState } from "react";
import {
  forceCollide,
  forceLink,
  forceManyBody,
  forceSimulation,
  forceX,
  forceY,
  type SimulationLinkDatum,
  type SimulationNodeDatum,
} from "d3-force";
import type { VaultGraph, VaultGraphNode } from "@/features/vault/types/vault";
import { attachPanZoom, identityTransform, type ViewTransform } from "@/shared/utils/pan-zoom";
import styles from "../vault.module.css";

interface VaultGraphCanvasProps {
  graph: VaultGraph;
  selectedId: string | null;
  onSelect: (id: string) => void;
}

type SimNode = VaultGraphNode & SimulationNodeDatum;
type SimLink = SimulationLinkDatum<SimNode>;

/** CSS custom properties the folder hues cycle through, so both themes colour the graph. */
const HUES = ["--accent-cyan", "--accent-purple", "--accent-amber", "--accent-green", "--accent-blue", "--accent-red"];
const LABEL_ZOOM = 1.6;
const LABEL_MIN_DEGREE = 6;
const HIT_RADIUS = 10;
const MIN_ZOOM = 0.1;
const MAX_ZOOM = 8;

/**
 * Obsidian-style note graph: a force layout drawn on a 2D canvas (thousands of nodes stay smooth where SVG would
 * not). Drag pans, the wheel zooms, hover highlights a note's neighbours and a click opens it.
 */
export function VaultGraphCanvas({ graph, selectedId, onSelect }: VaultGraphCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [hovered, setHovered] = useState<SimNode | null>(null);
  const hoveredRef = useRef<SimNode | null>(null);
  const selectedRef = useRef(selectedId);
  const drawRef = useRef<() => void>(() => {});

  useEffect(() => {
    selectedRef.current = selectedId;
    drawRef.current();
  }, [selectedId]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) {
      return;
    }

    const nodes: SimNode[] = graph.nodes.map((n) => ({ ...n }));
    const links: SimLink[] = graph.edges.map((e) => ({ source: e.source, target: e.target }));
    const neighbours = buildNeighbours(graph);
    const palette = readPalette(canvas);
    const folderColour = colourByFolder(nodes, palette.hues);

    let transform: ViewTransform = identityTransform;
    let width = 0;
    let height = 0;

    const draw = () => {
      const ratio = window.devicePixelRatio || 1;
      context.setTransform(ratio, 0, 0, ratio, 0, 0);
      context.clearRect(0, 0, width, height);
      context.save();
      context.translate(transform.x, transform.y);
      context.scale(transform.k, transform.k);

      const focus = hoveredRef.current ?? nodes.find((n) => n.id === selectedRef.current) ?? null;
      const focusSet = focus ? neighbours.get(focus.id) : undefined;

      context.lineWidth = 0.6 / transform.k;
      for (const link of links) {
        const source = link.source as SimNode;
        const target = link.target as SimNode;
        const lit = focus !== null && (source.id === focus.id || target.id === focus.id);
        context.strokeStyle = lit ? palette.accent : palette.edge;
        context.globalAlpha = focus && !lit ? 0.25 : 1;
        context.beginPath();
        context.moveTo(source.x ?? 0, source.y ?? 0);
        context.lineTo(target.x ?? 0, target.y ?? 0);
        context.stroke();
      }

      for (const node of nodes) {
        const dimmed = focus !== null && node.id !== focus.id && !focusSet?.has(node.id);
        context.globalAlpha = dimmed ? 0.25 : 1;
        context.fillStyle = node.id === selectedRef.current ? palette.accent : folderColour(node);
        context.beginPath();
        context.arc(node.x ?? 0, node.y ?? 0, radius(node), 0, Math.PI * 2);
        context.fill();
      }

      context.globalAlpha = 1;
      context.fillStyle = palette.text;
      context.font = `${11 / transform.k}px ${palette.font}`;
      context.textAlign = "center";
      for (const node of nodes) {
        const isFocus = focus !== null && (node.id === focus.id || focusSet?.has(node.id));
        if (isFocus || transform.k >= LABEL_ZOOM || node.degree >= LABEL_MIN_DEGREE) {
          context.fillText(node.title, node.x ?? 0, (node.y ?? 0) + radius(node) + 11 / transform.k);
        }
      }

      context.restore();
    };
    drawRef.current = draw;

    const resize = () => {
      const ratio = window.devicePixelRatio || 1;
      width = canvas.clientWidth;
      height = canvas.clientHeight;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      draw();
    };

    const simulation = forceSimulation(nodes)
      .force("link", forceLink<SimNode, SimLink>(links).id((n) => n.id).distance(40).strength(0.4))
      .force("charge", forceManyBody().strength(-60).distanceMax(400))
      .force("x", forceX(0).strength(0.04))
      .force("y", forceY(0).strength(0.04))
      .force("collide", forceCollide<SimNode>((n) => radius(n) + 2))
      .on("tick", draw);

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      simulation.stop();
      simulation.tick(300);
    }

    const panZoom = attachPanZoom(canvas, {
      minScale: MIN_ZOOM,
      maxScale: MAX_ZOOM,
      onChange: (next) => {
        transform = next;
        draw();
      },
    });

    resize();
    panZoom.setTransform({ x: width / 2, y: height / 2, k: 1 });

    const findNode = (event: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      const [x, y] = panZoom.toWorld(event.clientX - rect.left, event.clientY - rect.top);
      return simulation.find(x, y, HIT_RADIUS / transform.k) ?? null;
    };

    const onMove = (event: MouseEvent) => {
      const node = findNode(event);
      if (node !== hoveredRef.current) {
        hoveredRef.current = node;
        setHovered(node);
        canvas.style.cursor = node ? "pointer" : "grab";
        draw();
      }
    };

    const onClick = (event: MouseEvent) => {
      if (panZoom.dragged) {
        return;
      }
      const node = findNode(event);
      if (node) {
        onSelect(node.id);
      }
    };

    const observer = new ResizeObserver(resize);
    observer.observe(canvas);
    canvas.addEventListener("mousemove", onMove);
    canvas.addEventListener("click", onClick);

    return () => {
      simulation.stop();
      observer.disconnect();
      panZoom.detach();
      canvas.removeEventListener("mousemove", onMove);
      canvas.removeEventListener("click", onClick);
      drawRef.current = () => {};
    };
  }, [graph, onSelect]);

  return (
    <div className={styles.graphWrap}>
      <canvas ref={canvasRef} className={styles.graphCanvas} role="img" aria-label={`Graph of ${graph.nodes.length} notes`} />
      {hovered && (
        <div className={styles.graphReadout} aria-live="polite">
          <span className={styles.noteRowTitle}>{hovered.title}</span>
          <span className={styles.notePath}>
            {hovered.path} · {hovered.degree} link{hovered.degree === 1 ? "" : "s"}
          </span>
        </div>
      )}
    </div>
  );
}

function radius(node: VaultGraphNode): number {
  return 3 + Math.min(Math.sqrt(node.degree) * 1.6, 9);
}

function buildNeighbours(graph: VaultGraph): Map<string, Set<string>> {
  const map = new Map<string, Set<string>>();
  for (const edge of graph.edges) {
    if (!map.has(edge.source)) map.set(edge.source, new Set());
    if (!map.has(edge.target)) map.set(edge.target, new Set());
    map.get(edge.source)!.add(edge.target);
    map.get(edge.target)!.add(edge.source);
  }
  return map;
}

function colourByFolder(nodes: VaultGraphNode[], hues: string[]): (node: VaultGraphNode) => string {
  const topFolders = [...new Set(nodes.map((n) => n.folder.split("/")[0] ?? ""))].sort();
  const index = new Map(topFolders.map((folder, i) => [folder, i]));
  return (node) => hues[(index.get(node.folder.split("/")[0] ?? "") ?? 0) % hues.length];
}

function readPalette(element: HTMLElement) {
  const style = getComputedStyle(element);
  const read = (name: string, fallback: string) => style.getPropertyValue(name).trim() || fallback;

  return {
    hues: HUES.map((name) => read(name, "#45c6da")),
    accent: read("--accent", "#7fd8b4"),
    edge: read("--border-strong", "rgba(200, 212, 235, 0.22)"),
    text: read("--text-secondary", "#a4abbb"),
    font: read("--font-mono", "monospace"),
  };
}
