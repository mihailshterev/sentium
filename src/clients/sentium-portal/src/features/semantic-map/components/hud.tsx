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
import styles from "../semantic-map.module.css";
import type { GraphNode, VisualizationMode } from "../engine";
import { COLLECTION_COLORS, getCollectionTheme } from "../engine";
import type { KnowledgeMapSearchResult } from "@/features/semantic-map/types/knowledge-map";

/*
 * Panel transitions animate opacity and transform only. Both stay on the compositor; a `filter: blur()`
 * transition would re-rasterise the panel every frame on top of the canvas animating underneath.
 */
const panelVariants = {
  hidden: { opacity: 0, y: 8 },
  visible: { opacity: 1, y: 0, transition: { duration: 0.22, ease: [0.16, 1, 0.3, 1] as const } },
  exit: { opacity: 0, y: 8, transition: { duration: 0.15, ease: "easeIn" as const } },
};

const slideInRight = {
  hidden: { opacity: 0, x: 16 },
  visible: { opacity: 1, x: 0, transition: { duration: 0.24, ease: [0.16, 1, 0.3, 1] as const } },
  exit: { opacity: 0, x: 16, transition: { duration: 0.15, ease: "easeIn" as const } },
};

const fadeIn = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: 0.2 } },
  exit: { opacity: 0, transition: { duration: 0.15 } },
};

const MODES: VisualizationMode[] = ["constellation", "neural", "cluster"];

const formatScore = (score: number) => `${Math.round(score * 100)}%`;

export function LoadingOverlay({ visible }: { visible: boolean }) {
  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className={styles.loadingOverlay}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0, transition: { duration: 0.3 } }}
          role="status"
        >
          <div className={styles.loadingInner}>
            <div className={styles.loadingOrb} />
            <Loader2 size={22} className={styles.loadingSpinner} aria-hidden="true" />
            <p className={styles.loadingText}>Loading knowledge map…</p>
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
      initial={{ y: -16, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
    >
      <div className={styles.toolbarLeft}>
        <div className={styles.brandBadge}>
          <Waypoints size={15} aria-hidden="true" />
          <span>sentium / semantic-map</span>
        </div>

        <AnimatePresence>
          {isDemo && (
            <motion.div className={styles.demoBadge} title="No indexed knowledge yet — showing sample data" {...fadeIn}>
              <Sparkles size={12} aria-hidden="true" />
              <span>Demo</span>
            </motion.div>
          )}
        </AnimatePresence>

        <div className={styles.statsRow}>
          <span className={styles.statChip} title="Knowledge base chunks">
            <Database size={12} style={{ color: COLLECTION_COLORS.knowledge_base.color }} aria-hidden="true" />
            {stats.kb} KB
          </span>
          <span className={styles.statChip} title="Agent learnings">
            <Brain size={12} style={{ color: COLLECTION_COLORS.agent_learnings.color }} aria-hidden="true" />
            {stats.learnings} Learnings
          </span>
          <span className={styles.statChip} title="User memories">
            <Sparkles size={12} style={{ color: COLLECTION_COLORS.user_memories.color }} aria-hidden="true" />
            {stats.memories} Memories
          </span>
        </div>
      </div>

      <div className={styles.toolbarCenter}>
        <form
          className={styles.searchWrap}
          role="search"
          onSubmit={(e) => {
            e.preventDefault();
            onSearchSubmit(searchQuery);
          }}
        >
          <Search size={15} className={styles.searchIcon} aria-hidden="true" />
          <input
            className={styles.searchInput}
            type="text"
            enterKeyHint="search"
            aria-label="Search the knowledge map"
            placeholder="Search by meaning, then press Enter"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
          />
          {isSearching && <Loader2 size={14} className={styles.searchSpinner} aria-label="Searching" />}
          {searchQuery && !isSearching && (
            <button
              type="button"
              className={`${styles.iconBtn} ${styles.searchClear}`}
              onClick={onSearchClear}
              aria-label="Clear search"
            >
              <X size={14} />
            </button>
          )}
        </form>
      </div>

      <div className={styles.toolbarRight}>
        <div className={styles.modeSelector} role="group" aria-label="Layout">
          {MODES.map((m) => (
            <button
              key={m}
              type="button"
              className={`${styles.modeBtn} ${mode === m ? styles.modeBtnActive : ""}`}
              aria-pressed={mode === m}
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
  legendVisible: boolean;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onReset: () => void;
  onToggleLegend: () => void;
}

export function ZoomControls({ legendVisible, onZoomIn, onZoomOut, onReset, onToggleLegend }: ZoomControlsProps) {
  return (
    <motion.div
      className={styles.zoomControls}
      initial={{ x: -16, opacity: 0 }}
      animate={{ x: 0, opacity: 1 }}
      transition={{ duration: 0.3, ease: [0.16, 1, 0.3, 1] }}
    >
      <button type="button" className={styles.zoomBtn} onClick={onZoomIn} title="Zoom in" aria-label="Zoom in">
        <ZoomIn size={16} />
      </button>
      <button type="button" className={styles.zoomBtn} onClick={onZoomOut} title="Zoom out" aria-label="Zoom out">
        <ZoomOut size={16} />
      </button>
      <button type="button" className={styles.zoomBtn} onClick={onReset} title="Reset view" aria-label="Reset view">
        <RotateCcw size={16} />
      </button>
      <button
        type="button"
        className={styles.zoomBtn}
        onClick={onToggleLegend}
        title="Toggle legend"
        aria-label="Toggle legend"
        aria-pressed={legendVisible}
      >
        <Layers size={16} />
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
                <span className={styles.legendDot} style={{ background: val.color }} />
                <span className={styles.legendLabel}>{val.label}</span>
              </div>
            ))}
          <div className={styles.legendDivider} />
          <p className={styles.legendHint}>Scroll to zoom · Drag to pan</p>
          <p className={styles.legendHint}>Click a node to inspect · Esc to close</p>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

interface ResultsPanelProps {
  /** Null while no search is active; an empty array is a search that matched nothing. */
  results: KnowledgeMapSearchResult[] | null;
  onClose: () => void;
  onSelectResult: (id: string) => void;
}

export function ResultsPanel({ results, onClose, onSelectResult }: ResultsPanelProps) {
  return (
    <AnimatePresence>
      {results && (
        <motion.section
          className={styles.resultsPanel}
          aria-label="Search results"
          variants={slideInRight}
          initial="hidden"
          animate="visible"
          exit="exit"
        >
          <div className={styles.resultsPanelHeader}>
            <span className={styles.resultsPanelTitle} role="status">
              <Search size={14} aria-hidden="true" />
              {results.length === 1 ? "1 result" : `${results.length} results`}
            </span>
            <button type="button" className={styles.iconBtn} onClick={onClose} aria-label="Close search results">
              <X size={14} />
            </button>
          </div>
          {results.length === 0 ? (
            <p className={styles.resultsEmpty}>Nothing matched. Try different wording or a broader question.</p>
          ) : (
            <ul className={styles.resultsList}>
              {results.map((r) => {
                const col = COLLECTION_COLORS[r.collection] ?? COLLECTION_COLORS.default;
                return (
                  <li key={r.id}>
                    <button type="button" className={styles.resultItem} onClick={() => onSelectResult(r.id)}>
                      <span className={styles.resultScore}>
                        <span className={styles.resultTrack}>
                          <span
                            className={styles.resultBar}
                            style={{ width: formatScore(r.score), background: col.color }}
                          />
                        </span>
                        <span className={styles.resultPercent}>{formatScore(r.score)}</span>
                      </span>
                      <span className={styles.resultContent}>{r.content}</span>
                      <span className={styles.resultSource}>
                        <span className={styles.legendDot} style={{ background: col.color }} />
                        {r.source}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </motion.section>
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
        <motion.section
          className={styles.detailPanel}
          aria-label="Selected node"
          variants={panelVariants}
          initial="hidden"
          animate="visible"
          exit="exit"
          key={node.id}
        >
          <div className={styles.detailHeader}>
            <div className={styles.detailHeaderLeft}>
              <span className={styles.detailCollectionDot} style={{ background: node.colorStr }} />
              <div className={styles.detailTitle}>
                <p className={styles.detailSource}>{node.source}</p>
                <p className={styles.detailMeta}>
                  {getCollectionTheme(node.collection).label} · {node.sourceType}
                </p>
              </div>
            </div>
            <button type="button" className={styles.iconBtn} onClick={onClose} aria-label="Close details">
              <X size={15} />
            </button>
          </div>

          {node.queryScore !== undefined && (
            <div className={styles.detailScore}>
              <span>Relevance</span>
              <div className={styles.detailScoreBar}>
                <div
                  className={styles.detailScoreFill}
                  style={{ background: node.colorStr, width: formatScore(node.queryScore) }}
                />
              </div>
              <span className={styles.resultPercent}>{formatScore(node.queryScore)}</span>
            </div>
          )}

          <p className={styles.detailContent}>{node.fullContent}</p>

          {Object.keys(node.metadata).length > 0 && (
            <div className={styles.detailMeta2}>
              <p className={styles.detailMetaTitle}>
                <Info size={12} aria-hidden="true" />
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
            <ChevronRight size={12} aria-hidden="true" />
            Ingested {new Date(node.createdAt).toLocaleString()}
          </p>
        </motion.section>
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
          role="alert"
        >
          <Info size={14} aria-hidden="true" />
          {error}
          <button type="button" onClick={onDismiss} aria-label="Dismiss">
            <X size={14} />
          </button>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
