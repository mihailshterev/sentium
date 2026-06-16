import { useNavigate } from "react-router";
import { Activity, Loader, RefreshCw, Search, ShieldOff, Terminal, XCircle } from "lucide-react";
import styles from "./sandbox.module.scss";
import { useSandboxExecutions } from "../../hooks/useSandboxExecutions";
import { useSandboxStats } from "../../hooks/useSandboxStats";
import PageHeader from "../../components/ui/page-header";
import StatCard from "../../components/ui/stat-card";
import EmptyState from "../../components/ui/empty-state";
import LoadMore from "../../components/ui/load-more";
import RunRow from "./components/run-row";
import type { SandboxLanguage, SandboxStatusFilter } from "../../types/sandbox";

const STATUS_FILTERS: { value: SandboxStatusFilter | null; label: string }[] = [
  { value: null, label: "All" },
  { value: "Succeeded", label: "Succeeded" },
  { value: "Failed", label: "Failed" },
  { value: "Denied", label: "Denied" },
];

const LANGUAGE_FILTERS: { value: SandboxLanguage | null; label: string }[] = [
  { value: null, label: "All" },
  { value: "Python", label: "Python" },
  { value: "Node", label: "Node" },
];

const Sandbox = () => {
  const navigate = useNavigate();
  const {
    executions,
    hasMore,
    loadMore,
    isLoadingMore,
    status,
    setStatus,
    language,
    setLanguage,
    search,
    setSearch,
    isLoading,
    isFetching,
    refetch,
  } = useSandboxExecutions();
  const { stats } = useSandboxStats();

  const openRun = (jobId: string) => navigate(`/sandbox/${jobId}`);

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<Terminal size={18} className={styles.titleIcon} />}
        title="Sandbox Inspector"
        subtitle="Read-only audit view of agent-initiated code executions"
        right={
          <div className={styles.headerActions}>
            <div className={styles.liveTag}>
              <span className={styles.liveDot} />
              Live
            </div>
            <button className={styles.refreshBtn} onClick={() => refetch()} disabled={isFetching}>
              <RefreshCw size={13} className={isFetching ? styles.spinning : undefined} />
              Refresh
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <div className={styles.statsRow}>
          <StatCard icon={<Activity size={16} />} value={stats?.total ?? "—"} label="Total Runs" iconColor="blue" />
          <StatCard icon={<Activity size={16} />} value={stats?.succeeded ?? "—"} label="Succeeded" iconColor="green" />
          <StatCard icon={<XCircle size={16} />} value={stats?.failed ?? "—"} label="Failed" iconColor="red" />
          <StatCard
            icon={<ShieldOff size={16} />}
            value={stats?.denied ?? "—"}
            label="Policy Denied"
            iconColor="amber"
          />
        </div>

        <div className={styles.toolbar}>
          <div className={styles.searchWrap}>
            <Search size={14} className={styles.searchIcon} />
            <input
              className={styles.searchInput}
              placeholder="Search by agent or job id…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div className={styles.filterGroup}>
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.label}
                className={`${styles.filterChip} ${status === f.value ? styles.filterChipActive : ""}`}
                onClick={() => setStatus(f.value)}
              >
                {f.label}
              </button>
            ))}
          </div>

          <div className={styles.filterGroup}>
            {LANGUAGE_FILTERS.map((f) => (
              <button
                key={f.label}
                className={`${styles.filterChip} ${language === f.value ? styles.filterChipActive : ""}`}
                onClick={() => setLanguage(f.value)}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        <div className={styles.runsTable}>
          <div className={styles.runsTableHead}>
            <span>Status</span>
            <span>Agent</span>
            <span>Language</span>
            <span>Started</span>
            <span>Duration</span>
            <span>Exit</span>
            <span>Artifacts</span>
            <span />
          </div>

          <div className={styles.runsBody}>
            {isLoading && executions.length === 0 && (
              <div className={styles.runsEmpty}>
                <Loader size={18} className={styles.spinning} />
              </div>
            )}
            {!isLoading && executions.length === 0 && (
              <EmptyState
                icon={<Terminal size={32} />}
                title="No executions found"
                hint="Runs appear here as agents execute code, or adjust your filters."
              />
            )}
            {executions.map((run) => (
              <RunRow key={run.jobId} run={run} onOpen={openRun} />
            ))}
            <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
          </div>
        </div>
      </div>
    </div>
  );
};

export default Sandbox;
