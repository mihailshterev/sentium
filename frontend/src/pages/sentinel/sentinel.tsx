import { useState, useCallback } from "react";
import { useNavigate } from "react-router";
import { ShieldAlert, RefreshCw, AlertCircle, Zap, Eye, Radio, Globe, ArrowRight, WifiOff, Brain } from "lucide-react";
import styles from "./sentinel.module.scss";
import useSentinelEvents from "../../hooks/useSentinelEvents";
import { triggerNetworkAnalysis } from "../../services/agentRuntime.service";
import type { NetworkEvent } from "../../types/sentinel";

type ActionFilter = "all" | "Immediate-Review" | "Investigate";

function formatRelativeTime(timestamp: string): string {
  const diff = Date.now() - new Date(timestamp).getTime();
  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return new Date(timestamp).toLocaleDateString("en-GB", { day: "2-digit", month: "2-digit", year: "numeric" });
}

function parseScore(mlScore: string): number {
  return parseFloat(mlScore.replace("%", "").trim());
}

function getScoreClass(mlScore: string, s: CSSModuleClasses): string {
  const val = parseScore(mlScore);
  if (val >= 95) return s.scoreRed;
  if (val >= 80) return s.scoreAmber;
  return s.scoreGreen;
}

function getProtoClass(proto: string, s: CSSModuleClasses): string {
  switch (proto.toLowerCase()) {
    case "tcp":
      return s.protoTcp;
    case "udp":
      return s.protoUdp;
    case "icmp":
      return s.protoIcmp;
    default:
      return s.protoDefault;
  }
}

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className={styles.skeletonRow}>
          <div className={styles.skeletonCell} style={{ width: "5rem" }} />
          <div className={styles.skeletonCell} style={{ width: "9rem" }} />
          <div className={styles.skeletonCell} style={{ width: "9rem" }} />
          <div className={styles.skeletonCell} style={{ width: "3.5rem" }} />
          <div className={styles.skeletonCell} style={{ width: "4rem" }} />
          <div className={styles.skeletonCell} style={{ width: "4.5rem" }} />
          <div className={styles.skeletonCell} style={{ width: "5.5rem" }} />
        </div>
      ))}
    </>
  );
}

function EventRow({ event, onAnalyze }: { event: NetworkEvent; onAnalyze: (event: NetworkEvent) => Promise<void> }) {
  const isImmediate = event.action === "Immediate-Review";
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  const handleAnalyze = async () => {
    setIsAnalyzing(true);
    try {
      await onAnalyze(event);
    } finally {
      setIsAnalyzing(false);
    }
  };

  return (
    <div className={styles.eventRow}>
      <span className={styles.cellTime}>{formatRelativeTime(event.timestamp)}</span>
      <span className={`${styles.cellIp} ${styles.cellMono}`}>{event.origH}</span>
      <span className={styles.cellArrow}>
        <ArrowRight size={11} />
      </span>
      <span className={`${styles.cellIp} ${styles.cellMono}`}>{event.respH}</span>
      <span className={`${styles.badge} ${getProtoClass(event.proto, styles)}`}>{event.proto.toUpperCase()}</span>
      <span className={`${styles.badge} ${styles.badgeService}`}>
        {event.service !== "unknown" ? event.service : "-"}
      </span>
      <span className={`${styles.scoreCell} ${getScoreClass(event.mlScore, styles)}`}>{event.mlScore}</span>
      <span className={`${styles.actionBadge} ${isImmediate ? styles.actionImmediate : styles.actionInvestigate}`}>
        {isImmediate ? "REVIEW" : "INVEST"}
      </span>
      <button
        className={`${styles.analyzeBtn} ${isAnalyzing ? styles.analyzeBtnLoading : ""}`}
        onClick={handleAnalyze}
        disabled={isAnalyzing}
        title="Send to AI network analysis workflow"
      >
        <Brain size={10} />
        {isAnalyzing ? "..." : "Analyze"}
      </button>
    </div>
  );
}

type CSSModuleClasses = typeof styles;

const Sentinel = () => {
  const { events, isLoading, isRefetching, error, refetch } = useSentinelEvents();
  const [filter, setFilter] = useState<ActionFilter>("all");
  const [isManualRefetching, setIsManualRefetching] = useState(false);
  const navigate = useNavigate();

  const handleRefresh = async () => {
    setIsManualRefetching(true);
    await refetch();
    setIsManualRefetching(false);
  };

  const handleAnalyze = useCallback(
    async (event: NetworkEvent) => {
      const { eventId } = await triggerNetworkAnalysis(event);
      navigate(`/orchestration?autoStream=${encodeURIComponent(eventId)}`);
    },
    [navigate],
  );

  const immediateCount = events.filter((e) => e.action === "Immediate-Review").length;
  const investigateCount = events.filter((e) => e.action === "Investigate").length;
  const uniqueSources = new Set(events.map((e) => e.origH)).size;

  const filteredEvents = filter === "all" ? events : events.filter((e) => e.action === filter);

  if (error && events.length === 0) {
    return (
      <div className={styles.root}>
        <div className={styles.header}>
          <div className={styles.headerLeft}>
            <div className={styles.headerIcon}>
              <ShieldAlert size={18} />
            </div>
            <div>
              <h1 className={styles.pageTitle}>Sentinel</h1>
              <p className={styles.pageSubtitle}>Network security monitoring</p>
            </div>
          </div>
        </div>
        <div className={styles.errorState}>
          <AlertCircle size={32} className={styles.errorIcon} />
          <span className={styles.errorMessage}>
            Unable to load network events: {error instanceof Error ? error.message : "Unknown error"}
          </span>
          <button className={styles.retryBtn} onClick={handleRefresh}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <div className={styles.headerIcon}>
            <ShieldAlert size={18} />
          </div>
          <div>
            <h1 className={styles.pageTitle}>Sentinel</h1>
            <p className={styles.pageSubtitle}>Network anomaly detection via Zeek + ML analysis</p>
          </div>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.liveBadge}>
            <span className={styles.liveDot} />
            Live
          </div>
          <button
            className={`${styles.refreshBtn} ${isManualRefetching || isRefetching ? styles.spinning : ""}`}
            onClick={handleRefresh}
          >
            <RefreshCw size={12} />
            Refresh
          </button>
        </div>
      </div>

      <div className={styles.statsRow}>
        {isLoading ? (
          <>
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
            <div className={styles.skeletonCard} />
          </>
        ) : (
          <>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconBlue}`}>
                <Radio size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{events.length}</span>
                <span className={styles.statLabel}>Total Events</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconRed}`}>
                <Zap size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{immediateCount}</span>
                <span className={styles.statLabel}>Immediate Reviews</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconAmber}`}>
                <Eye size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{investigateCount}</span>
                <span className={styles.statLabel}>Investigations</span>
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={`${styles.statIcon} ${styles.iconGreen}`}>
                <Globe size={18} />
              </div>
              <div className={styles.statContent}>
                <span className={styles.statValue}>{uniqueSources}</span>
                <span className={styles.statLabel}>Unique Sources</span>
              </div>
            </div>
          </>
        )}
      </div>

      <div className={styles.body}>
        <div className={styles.section}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionTitle}>
              <Radio size={13} className={styles.sectionTitleIcon} />
              Network Anomaly Log
            </div>
            <div className={styles.filterTabs}>
              <button
                className={`${styles.filterTab} ${filter === "all" ? styles.filterTabActive : ""}`}
                onClick={() => setFilter("all")}
              >
                All
                {events.length > 0 && <span className={styles.filterCount}>{events.length}</span>}
              </button>
              <button
                className={`${styles.filterTab} ${filter === "Immediate-Review" ? styles.filterTabActive : ""}`}
                onClick={() => setFilter("Immediate-Review")}
              >
                Immediate
                {immediateCount > 0 && (
                  <span className={`${styles.filterCount} ${styles.filterCountRed}`}>{immediateCount}</span>
                )}
              </button>
              <button
                className={`${styles.filterTab} ${filter === "Investigate" ? styles.filterTabActive : ""}`}
                onClick={() => setFilter("Investigate")}
              >
                Investigate
                {investigateCount > 0 && (
                  <span className={`${styles.filterCount} ${styles.filterCountAmber}`}>{investigateCount}</span>
                )}
              </button>
            </div>
          </div>

          <div className={styles.tableHeader}>
            <span className={styles.colTime}>Time</span>
            <span className={styles.colIp}>Source IP</span>
            <span className={styles.colArrowSpacer} />
            <span className={styles.colIp}>Destination IP</span>
            <span className={styles.colProto}>Proto</span>
            <span className={styles.colService}>Service</span>
            <span className={styles.colScore}>Score</span>
            <span className={styles.colAction}>Action</span>
            <span className={styles.colAnalyze} />
          </div>

          <div className={styles.tableBody}>
            {isLoading ? (
              <SkeletonRows />
            ) : filteredEvents.length === 0 ? (
              <div className={styles.emptyState}>
                <WifiOff size={28} className={styles.emptyIcon} />
                <span className={styles.emptyTitle}>No events detected</span>
                <span className={styles.emptySubtitle}>
                  {filter === "all"
                    ? "Waiting for Zeek network traffic anomalies..."
                    : `No ${filter === "Immediate-Review" ? "immediate review" : "investigation"} events`}
                </span>
              </div>
            ) : (
              filteredEvents.map((event) => <EventRow key={event.id} event={event} onAnalyze={handleAnalyze} />)
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Sentinel;
