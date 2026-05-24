import { motion, AnimatePresence } from "framer-motion";
import {
  Waypoints,
  Search,
  ZoomIn,
  ZoomOut,
  RotateCcw,
  X,
  Loader2,
  Layers,
  Brain,
  Database,
  Sparkles,
  ChevronRight,
  Info,
} from "lucide-react";
import styles from "./semantic-map.module.scss";
import type { GraphNode, VisualizationMode } from "./engine";
import { COLLECTION_COLORS } from "./engine";
import type { KnowledgeMapSearchResult } from "../../types/knowledge-map";

const panelVariants = {
  hidden: { opacity: 0, scale: 0.96, y: 8, filter: "blur(8px)" },
  visible: {
    opacity: 1,
    scale: 1,
    y: 0,
    filter: "blur(0px)",
    transition: { duration: 0.4, ease: [0.16, 1, 0.3, 1] as const },
  },
  exit: {
    opacity: 0,
    scale: 0.96,
    y: 8,
    filter: "blur(8px)",
    transition: { duration: 0.25, ease: "easeIn" as const },
  },
};

const slideInRight = {
  hidden: { opacity: 0, x: 30, filter: "blur(6px)" },
  visible: {
    opacity: 1,
    x: 0,
    filter: "blur(0px)",
    transition: { duration: 0.45, ease: [0.16, 1, 0.3, 1] as const },
  },
  exit: {
    opacity: 0,
    x: 30,
    filter: "blur(6px)",
    transition: { duration: 0.25, ease: "easeIn" as const },
  },
};

const fadeIn = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: 0.3 } },
  exit: { opacity: 0, transition: { duration: 0.2 } },
};

export function LoadingOverlay({ visible }: { visible: boolean }) {
  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className={styles.loadingOverlay}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0, transition: { duration: 0.6 } }}
        >
          <div className={styles.loadingInner}>
            <div className={styles.loadingOrb} />
            <Loader2 size={22} className={styles.loadingSpinner} />
            <p className={styles.loadingText}>Initializing semantic universe…</p>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

interface ToolbarProps {
  isDemo: boolean;
  stats: { total: number; kb: number; learnings: number; memories: number };
  searchQuery: string;
  isSearching: boolean;
  mode: VisualizationMode;
  onSearchChange: (q: string) => void;
  onSearchSubmit: (q: string) => void;
  onSearchClear: () => void;
  onModeChange: (m: VisualizationMode) => void;
}

export function Toolbar({
  isDemo,
  stats,
  searchQuery,
  isSearching,
  mode,
  onSearchChange,
  onSearchSubmit,
  onSearchClear,
  onModeChange,
}: ToolbarProps) {
  return (
    <motion.div
      className={styles.toolbar}
      initial={{ y: -60, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.6, ease: [0.16, 1, 0.3, 1], delay: 0.2 }}
    >
      <div className={styles.toolbarLeft}>
        <div className={styles.brandBadge}>
          <Waypoints size={15} />
          <span>Semantic Map</span>
        </div>

        <AnimatePresence>
          {isDemo && (
            <motion.div className={styles.demoBadge} {...fadeIn}>
              <Sparkles size={12} />
              <span>Demo</span>
            </motion.div>
          )}
        </AnimatePresence>

        <div className={styles.statsRow}>
          <span className={styles.statChip} style={{ color: COLLECTION_COLORS.knowledge_base.color }}>
            <Database size={11} />
            {stats.kb} KB
          </span>
          <span className={styles.statChip} style={{ color: COLLECTION_COLORS.agent_learnings.color }}>
            <Brain size={11} />
            {stats.learnings} Learnings
          </span>
          <span className={styles.statChip} style={{ color: COLLECTION_COLORS.user_memories.color }}>
            <Sparkles size={11} />
            {stats.memories} Memories
          </span>
        </div>
      </div>

      <div className={styles.toolbarCenter}>
        <div className={styles.searchWrap}>
          <Search size={14} className={styles.searchIcon} />
          <input
            className={styles.searchInput}
            placeholder="Semantic search — illuminate knowledge…"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") onSearchSubmit(searchQuery);
            }}
          />
          {isSearching && <Loader2 size={13} className={styles.searchSpinner} />}
          {searchQuery && !isSearching && (
            <button className={styles.searchClear} onClick={onSearchClear}>
              <X size={13} />
            </button>
          )}
        </div>
      </div>

      <div className={styles.toolbarRight}>
        <div className={styles.modeSelector}>
          {(["constellation", "neural", "cluster"] as VisualizationMode[]).map((m) => (
            <button
              key={m}
              className={`${styles.modeBtn} ${mode === m ? styles.modeBtnActive : ""}`}
              onClick={() => onModeChange(m)}
            >
              {m}
            </button>
          ))}
        </div>
      </div>
    </motion.div>
  );
}

interface ZoomControlsProps {
  onZoomIn: () => void;
  onZoomOut: () => void;
  onReset: () => void;
  onToggleLegend: () => void;
}

export function ZoomControls({ onZoomIn, onZoomOut, onReset, onToggleLegend }: ZoomControlsProps) {
  return (
    <motion.div
      className={styles.zoomControls}
      initial={{ x: -40, opacity: 0 }}
      animate={{ x: 0, opacity: 1 }}
      transition={{ duration: 0.5, ease: [0.16, 1, 0.3, 1], delay: 0.4 }}
    >
      <button className={styles.zoomBtn} onClick={onZoomIn} title="Zoom in">
        <ZoomIn size={15} />
      </button>
      <button className={styles.zoomBtn} onClick={onZoomOut} title="Zoom out">
        <ZoomOut size={15} />
      </button>
      <button className={styles.zoomBtn} onClick={onReset} title="Reset view">
        <RotateCcw size={15} />
      </button>
      <button className={styles.zoomBtn} onClick={onToggleLegend} title="Toggle legend">
        <Layers size={15} />
      </button>
    </motion.div>
  );
}

export function Legend({ visible }: { visible: boolean }) {
  return (
    <AnimatePresence>
      {visible && (
        <motion.div className={styles.legend} variants={panelVariants} initial="hidden" animate="visible" exit="exit">
          <p className={styles.legendTitle}>Collections</p>
          {Object.entries(COLLECTION_COLORS)
            .filter(([k]) => k !== "default")
            .map(([key, val]) => (
              <div key={key} className={styles.legendItem}>
                <span
                  className={styles.legendDot}
                  style={{ background: val.color, boxShadow: `0 0 6px ${val.color}` }}
                />
                <span className={styles.legendLabel}>{val.label}</span>
              </div>
            ))}
          <div className={styles.legendDivider} />
          <p className={styles.legendHint}>Scroll to zoom · Drag to pan</p>
          <p className={styles.legendHint}>Click node to inspect</p>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

interface ResultsPanelProps {
  results: KnowledgeMapSearchResult[] | null;
  onClose: () => void;
  onSelectResult: (id: string) => void;
}

export function ResultsPanel({ results, onClose, onSelectResult }: ResultsPanelProps) {
  return (
    <AnimatePresence>
      {results && results.length > 0 && (
        <motion.div
          className={styles.resultsPanel}
          variants={slideInRight}
          initial="hidden"
          animate="visible"
          exit="exit"
        >
          <div className={styles.resultsPanelHeader}>
            <span className={styles.resultsPanelTitle}>
              <Search size={13} />
              {results.length} results
            </span>
            <button className={styles.resultsClose} onClick={onClose}>
              <X size={13} />
            </button>
          </div>
          <div className={styles.resultsList}>
            {results.slice(0, 8).map((r, i) => {
              const col = COLLECTION_COLORS[r.collection] ?? COLLECTION_COLORS.default;
              return (
                <motion.div
                  key={r.id}
                  className={styles.resultItem}
                  onClick={() => onSelectResult(r.id)}
                  initial={{ opacity: 0, x: 15 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: i * 0.05, duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
                >
                  <div className={styles.resultScore}>
                    <div
                      className={styles.resultBar}
                      style={{ width: `${Math.round(r.score * 100)}%`, background: col.color }}
                    />
                    <span style={{ color: col.color }}>{Math.round(r.score * 100)}%</span>
                  </div>
                  <p className={styles.resultContent}>{r.content}</p>
                  <span className={styles.resultSource} style={{ color: col.color }}>
                    {r.source}
                  </span>
                </motion.div>
              );
            })}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

interface DetailPanelProps {
  node: GraphNode | null;
  onClose: () => void;
}

export function DetailPanel({ node, onClose }: DetailPanelProps) {
  return (
    <AnimatePresence>
      {node && (
        <motion.div
          className={styles.detailPanel}
          variants={panelVariants}
          initial="hidden"
          animate="visible"
          exit="exit"
          key={node.id}
        >
          <div className={styles.detailHeader}>
            <div className={styles.detailHeaderLeft}>
              <motion.span
                className={styles.detailCollectionDot}
                style={{ background: node.colorStr, boxShadow: `0 0 8px ${node.colorStr}` }}
                animate={{ scale: [1, 1.3, 1] }}
                transition={{ duration: 1.5, repeat: Infinity, ease: "easeInOut" }}
              />
              <div>
                <p className={styles.detailSource}>{node.source}</p>
                <p className={styles.detailMeta}>
                  {node.collection} · {node.sourceType}
                </p>
              </div>
            </div>
            <button className={styles.detailClose} onClick={onClose}>
              <X size={14} />
            </button>
          </div>

          {node.queryScore !== undefined && (
            <div className={styles.detailScore}>
              <span>Relevance</span>
              <div className={styles.detailScoreBar}>
                <motion.div
                  className={styles.detailScoreFill}
                  style={{ background: node.colorStr }}
                  initial={{ width: 0 }}
                  animate={{ width: `${Math.round(node.queryScore * 100)}%` }}
                  transition={{ duration: 0.8, ease: [0.16, 1, 0.3, 1], delay: 0.2 }}
                />
              </div>
              <span style={{ color: node.colorStr }}>{Math.round(node.queryScore * 100)}%</span>
            </div>
          )}

          <p className={styles.detailContent}>{node.fullContent}</p>

          {Object.keys(node.metadata).length > 0 && (
            <div className={styles.detailMeta2}>
              <p className={styles.detailMetaTitle}>
                <Info size={11} />
                Metadata
              </p>
              {Object.entries(node.metadata).map(([k, v]) => (
                <div key={k} className={styles.detailMetaRow}>
                  <span className={styles.detailMetaKey}>{k}</span>
                  <span className={styles.detailMetaVal}>{v}</span>
                </div>
              ))}
            </div>
          )}

          <p className={styles.detailDate}>
            <ChevronRight size={10} />
            Ingested {new Date(node.createdAt).toLocaleString()}
          </p>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

export function ErrorToast({ error, onDismiss }: { error: string | null; onDismiss: () => void }) {
  return (
    <AnimatePresence>
      {error && (
        <motion.div
          className={styles.errorToast}
          variants={panelVariants}
          initial="hidden"
          animate="visible"
          exit="exit"
        >
          <Info size={13} />
          {error}
          <button onClick={onDismiss}>
            <X size={12} />
          </button>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
