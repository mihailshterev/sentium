import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import styles from "../semantic-map.module.css";
import { useThemeStore, resolveTheme } from "@/shared/ui/state/theme-store";
import { fetchKnowledgeMapNodes, searchKnowledgeMap } from "@/features/semantic-map/api/semantic-map.service";
import {
  UniverseRenderer,
  buildGraphData,
  createSimulation,
  spawnClickParticles,
  spawnSearchParticles,
  spawnSearchWaves,
  DEMO_NODES,
} from "../engine";
import type { GraphNode, GraphLink, VisualizationMode } from "../engine";
import {
  LoadingOverlay,
  Toolbar,
  ZoomControls,
  Legend,
  ResultsPanel,
  DetailPanel,
  ErrorToast,
} from "../components/hud";
import type { KnowledgeMapNode, KnowledgeMapSearchResult } from "@/features/semantic-map/types/knowledge-map";

const NODE_LIMIT = 300;
const SEARCH_TOP_K = 20;
const SEARCH_FOCUS_ZOOM = 1.5;
const ZOOM_STEP = 1.4;
/** Zoom factor per wheel pixel; a mouse-wheel notch (~100px) zooms by about 13%. */
const WHEEL_ZOOM_RATE = 0.0012;
const WHEEL_LINE_PX = 33;
const MAX_WHEEL_DELTA = 240;
/** Pointer travel below this still counts as a click, not a pan. */
const DRAG_THRESHOLD_PX = 4;

interface Graph {
  /** The API nodes this graph was built from; a new array means a new graph. */
  source: KnowledgeMapNode[];
  nodes: GraphNode[];
  links: GraphLink[];
}

function useResizeObserver(ref: React.RefObject<HTMLElement | null>) {
  const [size, setSize] = useState({ width: 0, height: 0 });
  useEffect(() => {
    if (!ref.current) {
      return;
    }
    const ro = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      setSize({ width: Math.round(width), height: Math.round(height) });
    });
    ro.observe(ref.current);
    return () => ro.disconnect();
  }, [ref]);
  return size;
}

export function SemanticMapView() {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasHostRef = useRef<HTMLDivElement>(null);
  const { width, height } = useResizeObserver(containerRef);
  const hasSize = width > 0 && height > 0;

  const preference = useThemeStore((s) => s.preference);
  const isLight = resolveTheme(preference) === "light";

  const [rawNodes, setRawNodes] = useState<KnowledgeMapNode[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isDemo, setIsDemo] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [searchResults, setSearchResults] = useState<KnowledgeMapSearchResult[] | null>(null);

  const [selectedNode, setSelectedNode] = useState<GraphNode | null>(null);
  const [mode, setMode] = useState<VisualizationMode>("constellation");
  const [showLegend, setShowLegend] = useState(true);

  const rendererRef = useRef<UniverseRenderer | null>(null);
  const graphRef = useRef<Graph | null>(null);
  /** Viewport size the layout was built for. Later resizes only resize the canvas, never the layout. */
  const worldSizeRef = useRef<{ width: number; height: number } | null>(null);

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      setIsLoading(true);
      try {
        const res = await fetchKnowledgeMapNodes(NODE_LIMIT);

        if (cancelled) {
          return;
        }

        if (res.nodes.length === 0) {
          setRawNodes(DEMO_NODES);
          setIsDemo(true);
        } else {
          setRawNodes(res.nodes);
          setIsDemo(false);
        }
      } catch {
        if (cancelled) {
          return;
        }
        setRawNodes(DEMO_NODES);
        setIsDemo(true);
        setError("Could not reach the vector store — showing demo data.");
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    fetchData();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const host = canvasHostRef.current;
    const container = containerRef.current;
    if (!host || !container || !hasSize) {
      return;
    }

    const world = worldSizeRef.current ?? { width: container.clientWidth, height: container.clientHeight };
    worldSizeRef.current = world;

    const renderer = new UniverseRenderer();
    rendererRef.current = renderer;
    if (graphRef.current) {
      renderer.setGraph(graphRef.current.nodes, graphRef.current.links);
    }

    const startsLight = resolveTheme(useThemeStore.getState().preference) === "light";
    void renderer.init(host, world.width, world.height, startsLight).then(() => {
      // The container may have been resized while the renderer was still starting up.
      renderer.resize(container.clientWidth, container.clientHeight);
    });

    return () => {
      renderer.destroy();
      rendererRef.current = null;
    };
  }, [hasSize]);

  useEffect(() => {
    rendererRef.current?.resize(width, height);
  }, [width, height]);

  useEffect(() => {
    rendererRef.current?.setTheme(isLight);
  }, [isLight]);

  useEffect(() => {
    const world = worldSizeRef.current;
    if (!hasSize || !world || rawNodes.length === 0) {
      return;
    }

    // Switching the layout mode keeps the same graph (positions, selection, search highlights) and only
    // re-runs the forces; the graph itself is rebuilt when new data arrives.
    let graph = graphRef.current;
    if (graph?.source !== rawNodes) {
      graph = { source: rawNodes, ...buildGraphData(rawNodes) };
      graphRef.current = graph;
      rendererRef.current?.setGraph(graph.nodes, graph.links);
    }

    const sim = createSimulation(graph.nodes, graph.links, world.width, world.height, mode);
    sim.on("tick", () => rendererRef.current?.markLayoutDirty());

    return () => {
      sim.stop();
    };
  }, [rawNodes, hasSize, mode]);

  const selectNode = useCallback((node: GraphNode | null) => {
    setSelectedNode(node);
    const renderer = rendererRef.current;
    if (node && renderer) {
      spawnClickParticles(node, renderer.particles);
    }
  }, []);

  useEffect(() => {
    const canvas = canvasHostRef.current;
    if (!canvas) {
      return;
    }

    const pointer = { down: false, dragging: false, startX: 0, startY: 0, lastX: 0, lastY: 0 };

    const nodeUnder = (e: PointerEvent): GraphNode | null => {
      const rect = canvas.getBoundingClientRect();
      return rendererRef.current?.nodeAt(e.clientX - rect.left, e.clientY - rect.top) ?? null;
    };

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const pixels = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * WHEEL_LINE_PX : e.deltaY;
      const delta = Math.max(-MAX_WHEEL_DELTA, Math.min(MAX_WHEEL_DELTA, pixels));
      rendererRef.current?.zoomBy(Math.exp(-delta * WHEEL_ZOOM_RATE), e.clientX - rect.left, e.clientY - rect.top);
    };

    const onDown = (e: PointerEvent) => {
      if (e.button !== 0) {
        return;
      }
      pointer.down = true;
      pointer.dragging = false;
      pointer.startX = pointer.lastX = e.clientX;
      pointer.startY = pointer.lastY = e.clientY;
      canvas.setPointerCapture(e.pointerId);
    };

    const onMove = (e: PointerEvent) => {
      const renderer = rendererRef.current;
      if (!renderer) {
        return;
      }
      if (!pointer.down) {
        const node = nodeUnder(e);
        renderer.setHoveredId(node?.id ?? null);
        canvas.style.cursor = node ? "pointer" : "";
        return;
      }
      const travel = Math.hypot(e.clientX - pointer.startX, e.clientY - pointer.startY);
      if (!pointer.dragging && travel < DRAG_THRESHOLD_PX) {
        return;
      }
      if (!pointer.dragging) {
        pointer.dragging = true;
        renderer.setHoveredId(null);
        canvas.style.cursor = "grabbing";
      }
      renderer.panBy(e.clientX - pointer.lastX, e.clientY - pointer.lastY);
      pointer.lastX = e.clientX;
      pointer.lastY = e.clientY;
    };

    const onUp = (e: PointerEvent) => {
      if (!pointer.down) {
        return;
      }
      pointer.down = false;
      canvas.style.cursor = "";
      // A press that never turned into a drag is a click: select the node under it, or clear the selection.
      if (!pointer.dragging) {
        selectNode(nodeUnder(e));
      }
      pointer.dragging = false;
    };

    const onCancel = () => {
      pointer.down = false;
      pointer.dragging = false;
      canvas.style.cursor = "";
    };

    const onLeave = () => {
      if (!pointer.down) {
        rendererRef.current?.setHoveredId(null);
      }
    };

    canvas.addEventListener("wheel", onWheel, { passive: false });
    canvas.addEventListener("pointerdown", onDown);
    canvas.addEventListener("pointermove", onMove);
    canvas.addEventListener("pointerup", onUp);
    canvas.addEventListener("pointercancel", onCancel);
    canvas.addEventListener("pointerleave", onLeave);

    return () => {
      canvas.removeEventListener("wheel", onWheel);
      canvas.removeEventListener("pointerdown", onDown);
      canvas.removeEventListener("pointermove", onMove);
      canvas.removeEventListener("pointerup", onUp);
      canvas.removeEventListener("pointercancel", onCancel);
      canvas.removeEventListener("pointerleave", onLeave);
    };
  }, [selectNode]);

  const applyHighlights = useCallback((scoreById: Map<string, number>) => {
    for (const n of graphRef.current?.nodes ?? []) {
      // eslint-disable-next-line react-hooks/immutability
      n.highlighted = scoreById.has(n.id);
      n.queryScore = scoreById.get(n.id);
    }
    rendererRef.current?.setHasSearchResults(scoreById.size > 0);
  }, []);

  const handleSearch = useCallback(
    async (q: string) => {
      if (!q.trim()) {
        setSearchResults(null);
        applyHighlights(new Map());
        return;
      }

      setIsSearching(true);
      try {
        const res = await searchKnowledgeMap(q, SEARCH_TOP_K);
        setSearchResults(res.results);
        applyHighlights(new Map(res.results.map((r) => [r.id, r.score])));

        const renderer = rendererRef.current;
        const top = graphRef.current?.nodes.find((n) => n.id === res.results[0]?.id);
        if (renderer && top && top.x != null && top.y != null) {
          spawnSearchParticles(top, renderer.particles);
          for (const wave of spawnSearchWaves(top)) {
            renderer.addWave(wave);
          }
          renderer.flyTo(top.x, top.y, SEARCH_FOCUS_ZOOM);
        }
      } catch {
        setError("Search failed — please try again.");
      } finally {
        setIsSearching(false);
      }
    },
    [applyHighlights],
  );

  const clearSearch = useCallback(() => {
    setSearchQuery("");
    setSearchResults(null);
    applyHighlights(new Map());
  }, [applyHighlights]);

  const onSelectResult = useCallback(
    (id: string) => {
      const node = graphRef.current?.nodes.find((n) => n.id === id);
      if (!node) {
        return;
      }
      selectNode(node);
      if (node.x != null && node.y != null) {
        rendererRef.current?.flyTo(node.x, node.y);
      }
    },
    [selectNode],
  );

  useEffect(() => {
    rendererRef.current?.setSelectedId(selectedNode?.id ?? null);
  }, [selectedNode]);

  const hasSelection = selectedNode !== null;
  const hasSearch = searchResults !== null;
  useEffect(() => {
    if (!hasSelection && !hasSearch) {
      return;
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key !== "Escape") {
        return;
      }
      if (hasSelection) {
        setSelectedNode(null);
      } else {
        clearSearch();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [hasSelection, hasSearch, clearSearch]);

  const zoomIn = useCallback(() => rendererRef.current?.zoomBy(ZOOM_STEP), []);
  const zoomOut = useCallback(() => rendererRef.current?.zoomBy(1 / ZOOM_STEP), []);
  const resetZoom = useCallback(() => rendererRef.current?.resetCamera(), []);

  const stats = useMemo(
    () => ({
      total: rawNodes.length,
      kb: rawNodes.filter((n) => n.collection === "knowledge_base").length,
      learnings: rawNodes.filter((n) => n.collection === "agent_learnings").length,
      memories: rawNodes.filter((n) => n.collection === "user_memories").length,
    }),
    [rawNodes],
  );

  return (
    <div className={styles.root} ref={containerRef}>
      <div ref={canvasHostRef} className={styles.canvas} role="img" aria-label="Knowledge map" />

      <div className={styles.fogTop} />
      <div className={styles.fogBottom} />
      <div className={styles.vignette} />

      <LoadingOverlay visible={isLoading} />

      <Toolbar
        isDemo={isDemo}
        stats={stats}
        searchQuery={searchQuery}
        isSearching={isSearching}
        mode={mode}
        onSearchChange={setSearchQuery}
        onSearchSubmit={(q) => void handleSearch(q)}
        onSearchClear={clearSearch}
        onModeChange={setMode}
      />

      <ZoomControls
        legendVisible={showLegend}
        onZoomIn={zoomIn}
        onZoomOut={zoomOut}
        onReset={resetZoom}
        onToggleLegend={() => setShowLegend((v) => !v)}
      />

      <Legend visible={showLegend} />

      <ResultsPanel results={searchResults} onClose={clearSearch} onSelectResult={onSelectResult} />

      <DetailPanel node={selectedNode} onClose={() => setSelectedNode(null)} />

      <ErrorToast error={error} onDismiss={() => setError(null)} />
    </div>
  );
}
