import { useNavigate } from "react-router";
import { Activity, CheckCircle2, RefreshCw, ShieldOff, Terminal, XCircle } from "lucide-react";
import styles from "../sandbox.module.css";
import { useSandboxExecutions } from "@/features/sandbox/hooks/useSandboxExecutions";
import { useSandboxStats } from "@/features/sandbox/hooks/useSandboxStats";
import type { SandboxLanguage, SandboxStatusFilter } from "@/features/sandbox/types/sandbox";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { SearchInput } from "@/shared/ui/form-controls";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import { RunRow } from "../components/run-row";

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

const COLUMNS = 8;

function FilterGroup<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: { value: T | null; label: string }[];
  value: T | null;
  onChange: (value: T | null) => void;
}) {
  return (
    <div className={styles.filterGroup} role="group" aria-label={label}>
      {options.map((f) => (
        <button
          key={f.label}
          type="button"
          className={styles.filterChip}
          aria-pressed={value === f.value}
          onClick={() => onChange(f.value)}
        >
          {f.label}
        </button>
      ))}
    </div>
  );
}

export function SandboxView() {
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

  return (
    <Page
      title="Sandbox Inspector"
      subtitle="Read-only audit view of agent-initiated code executions"
      width="wide"
      actions={
        <>
          <Badge tone="success" dot>
            Live
          </Badge>
          <Button
            variant="secondary"
            icon={<RefreshCw size={15} className={isFetching ? "animate-spin" : undefined} />}
            onClick={() => refetch()}
            disabled={isFetching}
          >
            Refresh
          </Button>
        </>
      }
    >
      <StatGrid minCellWidth={180}>
        <StatCard icon={<Activity />} value={stats?.total ?? "—"} label="Total Runs" />
        <StatCard icon={<CheckCircle2 />} value={stats?.succeeded ?? "—"} label="Succeeded" iconColor="green" />
        <StatCard icon={<XCircle />} value={stats?.failed ?? "—"} label="Failed" iconColor="red" />
        <StatCard icon={<ShieldOff />} value={stats?.denied ?? "—"} label="Policy Denied" iconColor="amber" />
      </StatGrid>

      <Card padding="none" as="section">
        <div className={styles.toolbar}>
          <SearchInput
            placeholder="Search by agent or job id…"
            aria-label="Search runs"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            wrapperClassName={styles.search}
          />
          <FilterGroup label="Status" options={STATUS_FILTERS} value={status} onChange={setStatus} />
          <FilterGroup label="Language" options={LANGUAGE_FILTERS} value={language} onChange={setLanguage} />
        </div>

        <div className="data-table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Agent</th>
                <th>Language</th>
                <th>Started</th>
                <th className="num">Duration</th>
                <th>Exit</th>
                <th className="num">Artifacts</th>
                <th aria-label="Open" />
              </tr>
            </thead>
            <tbody>
              {isLoading && executions.length === 0 && (
                <tr>
                  <td colSpan={COLUMNS} className={styles.emptyCell}>
                    Loading runs…
                  </td>
                </tr>
              )}
              {executions.map((run) => (
                <RunRow key={run.jobId} run={run} onOpen={(jobId) => navigate(`/sandbox/${jobId}`)} />
              ))}
            </tbody>
          </table>
        </div>

        {!isLoading && executions.length === 0 && (
          <EmptyState
            icon={<Terminal />}
            title="No executions found"
            hint="Runs appear here as agents execute code, or adjust your filters."
          />
        )}

        <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
      </Card>
    </Page>
  );
}
