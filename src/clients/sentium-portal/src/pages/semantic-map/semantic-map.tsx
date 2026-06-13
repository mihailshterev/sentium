import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import gsap from "gsap";
import styles from "./semantic-map.module.scss";
import { useThemeStore, resolveTheme } from "../../stores/theme-store";
import { fetchKnowledgeMapNodes, searchKnowledgeMap } from "../../services/agentRuntime.service";
import {
  UniverseRenderer,
  buildGraphData,
  createSimulation,
  spawnClickParticles,
  spawnSearchParticles,
  spawnSearchWaves,
  DEMO_NODES,
} from "./engine";
import type { GraphNode, GraphLink, Particle, CameraState, VisualizationMode } from "./engine";
import { LoadingOverlay, Toolbar, ZoomControls, Legend, ResultsPanel, DetailPanel, ErrorToast } from "./hud";
import type * as d3Type from "d3";
import type { KnowledgeMapNode, KnowledgeMapSearchResult } from "../../types/knowledge-map";

function useResizeObserver(ref: React.RefObject<HTMLElement | null>) {
  const [size, setSize] = useState({ width: 0, height: 0 });
  useEffect(() => {
    if (!ref.current) {
      return;
    }
    const ro = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      setSize({ width, height });
    });
    ro.observe(ref.current);
    return () => ro.disconnect();
  }, [ref]);
  return size;
}

const SemanticMap = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const { width, height } = useResizeObserver(containerRef);

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
  const simRef = useRef<d3Type.Simulation<GraphNode, GraphLink> | null>(null);
  const graphNodesRef = useRef<GraphNode[]>([]);
  const graphLinksRef = useRef<GraphLink[]>([]);
  const particlesRef = useRef<Particle[]>([]);
  const cameraRef = useRef<CameraState>({
    x: 0,
    y: 0,
    zoom: 0.9,
    targetX: 0,
    targetY: 0,
    targetZoom: 0.9,
  });

  const dragRef = useRef<{
    dragging: boolean;
    lastX: number;
    lastY: number;
  }>({ dragging: false, lastX: 0, lastY: 0 });

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      setIsLoading(true);
      try {
        const res = await fetchKnowledgeMapNodes(300);

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
    if (!canvasRef.current || !width || !height) {
      return;
    }

    const renderer = new UniverseRenderer();
    rendererRef.current = renderer;

    renderer.onNodeHover = () => {};
    renderer.onNodeClick = (node) => {
      setSelectedNode(node);
      if (node) {
        spawnClickParticles(node, particlesRef.current);
        renderer.setParticles(particlesRef.current);
      }
    };

    renderer.init(canvasRef.current, width, height, isLight).then(() => {
      if (graphNodesRef.current.length > 0) {
        renderer.setGraph(graphNodesRef.current, graphLinksRef.current);
        renderer.setCamera(cameraRef.current);
      }
    });

    return () => {
      renderer.destroy();
      rendererRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps, react-hooks/refs
  }, [canvasRef.current]);

  useEffect(() => {
    rendererRef.current?.resize(width, height);
  }, [width, height]);

  useEffect(() => {
    rendererRef.current?.setTheme(isLight);
  }, [isLight]);

  useEffect(() => {
    if (!width || !height || rawNodes.length === 0) {
      return;
    }

    const { nodes, links } = buildGraphData(rawNodes);
    graphNodesRef.current = nodes;
    graphLinksRef.current = links;

    cameraRef.current.targetX = width / 2;
    cameraRef.current.targetY = height / 2;
    cameraRef.current.x = width / 2;
    cameraRef.current.y = height / 2;

    if (simRef.current) {
      simRef.current.stop();
    }

    const sim = createSimulation(nodes, links, width, height, mode);

    sim.on("tick", () => {
      const renderer = rendererRef.current;
      if (!renderer) {
        return;
      }
      renderer.setGraph(nodes, links);
      renderer.setParticles(particlesRef.current);
      renderer.setCamera(cameraRef.current);
    });

    simRef.current = sim;

    rendererRef.current?.setGraph(nodes, links);
    rendererRef.current?.setCamera(cameraRef.current);

    return () => {
      sim.stop();
    };
  }, [rawNodes, width, height, mode]);

  useEffect(() => {
    if (!canvasRef.current) {
      return;
    }
    const canvas = canvasRef.current;

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const cam = cameraRef.current;
      const factor = e.deltaY > 0 ? 0.9 : 1.1;
      cam.targetZoom = Math.max(0.15, Math.min(6, cam.targetZoom * factor));
    };

    canvas.addEventListener("wheel", onWheel, { passive: false });
    return () => canvas.removeEventListener("wheel", onWheel);
    // eslint-disable-next-line react-hooks/exhaustive-deps, react-hooks/refs
  }, [canvasRef.current]);

  useEffect(() => {
    if (!canvasRef.current) {
      return;
    }
    const canvas = canvasRef.current;

    const onDown = (e: PointerEvent) => {
      dragRef.current = { dragging: true, lastX: e.clientX, lastY: e.clientY };
      canvas.setPointerCapture(e.pointerId);
    };

    const onMove = (e: PointerEvent) => {
      const d = dragRef.current;
      if (!d.dragging) {
        return;
      }
      const cam = cameraRef.current;
      const dx = e.clientX - d.lastX;
      const dy = e.clientY - d.lastY;
      cam.targetX -= dx / cam.zoom;
      cam.targetY -= dy / cam.zoom;
      d.lastX = e.clientX;
      d.lastY = e.clientY;
    };

    const onUp = () => {
      dragRef.current.dragging = false;
    };

    canvas.addEventListener("pointerdown", onDown);
    canvas.addEventListener("pointermove", onMove);
    canvas.addEventListener("pointerup", onUp);
    canvas.addEventListener("pointerleave", onUp);

    return () => {
      canvas.removeEventListener("pointerdown", onDown);
      canvas.removeEventListener("pointermove", onMove);
      canvas.removeEventListener("pointerup", onUp);
      canvas.removeEventListener("pointerleave", onUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps, react-hooks/refs
  }, [canvasRef.current]);

  const handleSearch = useCallback(async (q: string) => {
    if (!q.trim()) {
      setSearchResults(null);
      for (const n of graphNodesRef.current) {
        // eslint-disable-next-line react-hooks/immutability
        n.highlighted = false;
        n.queryScore = undefined;
      }
      rendererRef.current?.setHasSearchResults(false);
      return;
    }

    setIsSearching(true);
    try {
      const res = await searchKnowledgeMap(q, 20);
      setSearchResults(res.results);

      const hitIds = new Set(res.results.map((r) => r.id));
      const scoreMap = new Map(res.results.map((r) => [r.id, r.score]));

      for (const n of graphNodesRef.current) {
        n.highlighted = hitIds.has(n.id);
        n.queryScore = scoreMap.get(n.id);
      }

      rendererRef.current?.setHasSearchResults(true);

      const top = graphNodesRef.current.find((n) => n.id === res.results[0]?.id);
      if (top && top.x && top.y) {
        spawnSearchParticles(top, particlesRef.current);
        const renderer = rendererRef.current;
        if (renderer) {
          for (const wave of spawnSearchWaves(top)) {
            renderer.addWave(wave);
          }
          renderer.setParticles(particlesRef.current);
        }

        gsap.to(cameraRef.current, {
          targetX: top.x,
          targetY: top.y,
          targetZoom: 1.5,
          duration: 1.2,
          ease: "power3.inOut",
        });
      }
    } catch {
      // fail silently — demo mode
    } finally {
      setIsSearching(false);
    }
  }, []);

  const clearSearch = useCallback(() => {
    setSearchQuery("");
    setSearchResults(null);
    for (const n of graphNodesRef.current) {
      // eslint-disable-next-line react-hooks/immutability
      n.highlighted = false;
      n.queryScore = undefined;
    }
    rendererRef.current?.setHasSearchResults(false);
  }, []);

  const onSelectResult = useCallback((id: string) => {
    const gn = graphNodesRef.current.find((n) => n.id === id);
    if (gn) {
      setSelectedNode(gn);
      spawnClickParticles(gn, particlesRef.current);
      rendererRef.current?.setParticles(particlesRef.current);
      rendererRef.current?.setSelectedId(gn.id);

      if (gn.x != null && gn.y != null) {
        gsap.to(cameraRef.current, {
          targetX: gn.x,
          targetY: gn.y,
          duration: 0.8,
          ease: "power2.out",
        });
      }
    }
  }, []);

  useEffect(() => {
    rendererRef.current?.setSelectedId(selectedNode?.id ?? null);
  }, [selectedNode]);

  const zoomIn = useCallback(() => {
    gsap.to(cameraRef.current, {
      targetZoom: Math.min(6, cameraRef.current.targetZoom * 1.4),
      duration: 0.4,
      ease: "power2.out",
    });
  }, []);

  const zoomOut = useCallback(() => {
    gsap.to(cameraRef.current, {
      targetZoom: Math.max(0.15, cameraRef.current.targetZoom / 1.4),
      duration: 0.4,
      ease: "power2.out",
    });
  }, []);

  const resetZoom = useCallback(() => {
    gsap.to(cameraRef.current, {
      targetX: width / 2,
      targetY: height / 2,
      targetZoom: 0.9,
      duration: 0.8,
      ease: "power3.inOut",
    });
  }, [width, height]);

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
      <canvas ref={canvasRef} className={styles.canvas} />

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
};

export default SemanticMap;
