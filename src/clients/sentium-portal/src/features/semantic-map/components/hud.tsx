import { Animated, EASE_IN, EASE_OUT_EXPO, Presence } from "@/shared/ui/motion";
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
const PANEL_MOTION = {
  from: { opacity: 0, y: 8 },
  exitTo: { opacity: 0, y: 8 },
  enter: { duration: 220, easing: EASE_OUT_EXPO },
  exit: { duration: 150, easing: EASE_IN },
};

const SLIDE_IN_RIGHT_MOTION = {
  from: { opacity: 0, x: 16 },
  exitTo: { opacity: 0, x: 16 },
  enter: { duration: 240, easing: EASE_OUT_EXPO },
  exit: { duration: 150, easing: EASE_IN },
};

const FADE_MOTION = {
  from: { opacity: 0 },
  exitTo: { opacity: 0 },
  enter: { duration: 200 },
  exit: { duration: 150 },
};

const ENTER_FROM_EDGE = { duration: 300, easing: EASE_OUT_EXPO };

const MODES: VisualizationMode[] = ["constellation", "neural", "cluster"];

const formatScore = (score: number) => `${Math.round(score * 100)}%`;

export function LoadingOverlay({ visible }: { visible: boolean }) {
  return (
    <Presence>
      {visible && (
        <Animated
          className={styles.loadingOverlay}
          from={{ opacity: 0 }}
          exitTo={{ opacity: 0 }}
          role="status"
        >
          <div className={styles.loadingInner}>
            <div className={styles.loadingOrb} />
            <Loader2 size={22} className={styles.loadingSpinner} aria-hidden="true" />
            <p className={styles.loadingText}>Loading knowledge map…</p>
          </div>
        </Animated>
      )}
    </Presence>
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
    <Animated className={styles.toolbar} from={{ y: -16, opacity: 0 }} enter={ENTER_FROM_EDGE}>
      <div className={styles.toolbarLeft}>
        <div className={styles.brandBadge}>
          <Waypoints size={15} aria-hidden="true" />
          <span>sentium / semantic-map</span>
        </div>

        <Presence>
          {isDemo && (
            <Animated className={styles.demoBadge} title="No indexed knowledge yet — showing sample data" {...FADE_MOTION}>
              <Sparkles size={12} aria-hidden="true" />
              <span>Demo</span>
            </Animated>
          )}
        </Presence>

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
    </Animated>
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
    <Animated
      className={styles.zoomControls}
      from={{ x: -16, opacity: 0 }}
      enter={ENTER_FROM_EDGE}
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
    </Animated>
  );
}

export function Legend({ visible }: { visible: boolean }) {
  return (
    <Presence>
      {visible && (
        <Animated className={styles.legend} {...PANEL_MOTION}>
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
        </Animated>
      )}
    </Presence>
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
    <Presence>
      {results && (
        <Animated
          as="section"
          className={styles.resultsPanel}
          aria-label="Search results"
          {...SLIDE_IN_RIGHT_MOTION}
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
        </Animated>
      )}
    </Presence>
  );
}

interface DetailPanelProps {
  node: GraphNode | null;
  onClose: () => void;
}

export function DetailPanel({ node, onClose }: DetailPanelProps) {
  return (
    <Presence>
      {node && (
        <Animated
          as="section"
          className={styles.detailPanel}
          aria-label="Selected node"
          {...PANEL_MOTION}
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
        </Animated>
      )}
    </Presence>
  );
}

export function ErrorToast({ error, onDismiss }: { error: string | null; onDismiss: () => void }) {
  return (
    <Presence>
      {error && (
        <Animated
          className={styles.errorToast}
          {...PANEL_MOTION}
          role="alert"
        >
          <Info size={14} aria-hidden="true" />
          {error}
          <button type="button" onClick={onDismiss} aria-label="Dismiss">
            <X size={14} />
          </button>
        </Animated>
      )}
    </Presence>
  );
}
