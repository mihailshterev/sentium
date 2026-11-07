import { useState } from "react";
import { Bot, BrainCircuit, Database, FlaskConical, RefreshCw, Trash2 } from "lucide-react";
import styles from "../knowledge-base.module.css";
import { useAgentLearnings } from "@/features/knowledge-base/hooks/useAgentLearnings";
import { useKnowledgeBaseStats } from "@/features/knowledge-base/hooks/useKnowledgeBaseStats";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Select } from "@/shared/ui/form-controls";
import { LoadMore } from "@/shared/ui/load-more";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { LearningCard } from "./learning-card";

const formatDate = (iso: string) => {
  const d = new Date(iso);
  return (
    d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) +
    " " +
    d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
  );
};

function Metric({ value, label, accent = false }: { value: React.ReactNode; label: string; accent?: boolean }) {
  return (
    <div className={styles.metric}>
      <span className={`${styles.metricValue} ${accent ? styles.metricAccent : ""}`}>{value}</span>
      <span className={styles.metricLabel}>{label}</span>
    </div>
  );
}

export const GlobalContextTab = () => {
  const { collections, isLoading, error, refetch, deleteCollection, isDeleting } = useKnowledgeBaseStats();
  const { stats, isStatsLoading } = useAgentLearnings();
  const [pendingDelete, setPendingDelete] = useState<string | null>(null);

  const totalVectors = collections.reduce((sum, c) => sum + c.pointCount, 0);

  return (
    <>
      <Card padding="none" as="section">
        <CardHeader
          icon={<FlaskConical />}
          title="Knowledge Base Overview"
          description="Live statistics from the vector store"
          actions={
            <Button
              variant="secondary"
              size="sm"
              icon={<RefreshCw size={14} className={isLoading ? "animate-spin" : undefined} />}
              onClick={() => void refetch()}
              disabled={isLoading}
            >
              Refresh
            </Button>
          }
        />

        <div className={styles.cardBody}>
          {isLoading && (
            <div aria-busy="true">
              <span className="visually-hidden">Querying vector store…</span>
              <SkeletonText lines={3} />
            </div>
          )}

          {error && <StatusMessage variant="error" message={error.message} />}

          {!isLoading && !error && (
            <>
              <div className={styles.metrics}>
                <Metric value={collections.length} label="Collections" />
                <Metric value={totalVectors.toLocaleString()} label="Total Vectors" />
                {!isStatsLoading && stats && (
                  <>
                    <Metric value={stats.totalLearnings} label="Agent Learnings" />
                    <Metric value={stats.globalLearnings} label="Global (Shared)" accent />
                    <Metric value={stats.pendingIngestion} label="Pending Ingestion" />
                  </>
                )}
              </div>

              {collections.length > 0 ? (
                <ul className={styles.collectionList}>
                  {collections.map((c) => (
                    <li key={c.collectionName} className={styles.collectionRow}>
                      <span className={styles.collectionIcon} aria-hidden="true">
                        <Database size={15} />
                      </span>
                      <span className={styles.collectionName}>{c.collectionName}</span>
                      <span className={styles.collectionMeta}>
                        <span className={styles.metaChip}>{c.pointCount.toLocaleString()} vectors</span>
                        <span className={styles.metaChip}>{c.vectorSize}‑dim</span>
                        <span className={styles.metaChip}>{c.distanceMetric}</span>
                      </span>
                      <Badge tone={c.pointCount > 0 ? "success" : "warning"}>
                        {c.pointCount > 0 ? "Active" : "Empty"}
                      </Badge>
                      <Button
                        variant="ghost"
                        size="sm"
                        iconOnly
                        icon={<Trash2 size={15} />}
                        onClick={() => setPendingDelete(c.collectionName)}
                        disabled={isDeleting}
                        title="Delete Collection"
                        className={styles.deleteButton}
                      />
                    </li>
                  ))}
                </ul>
              ) : (
                <EmptyState
                  compact
                  icon={<Database />}
                  title="No collections found"
                  hint="Collections are created automatically when documents are first ingested."
                />
              )}
            </>
          )}
        </div>
      </Card>

      {pendingDelete && (
        <ConfirmDialog
          open
          variant="danger"
          title="Delete collection?"
          description={`This will permanently delete the collection "${pendingDelete}" and all its vectors. This action cannot be undone.`}
          confirmLabel="Delete collection"
          onConfirm={() => {
            deleteCollection(pendingDelete);
            setPendingDelete(null);
          }}
          onCancel={() => setPendingDelete(null)}
        />
      )}

      {!isStatsLoading && stats && Object.keys(stats.learningsByAgent).length > 0 && (
        <Card padding="none" as="section">
          <CardHeader icon={<Bot />} title="Learnings by Agent" description="Insights captured per agent role" />
          <div className={styles.cardBody}>
            <div className={styles.metrics}>
              {Object.entries(stats.learningsByAgent).map(([agent, count]) => (
                <Metric key={agent} value={count} label={agent} />
              ))}
            </div>
          </div>
        </Card>
      )}
    </>
  );
};

export const AgentLearningsTab = () => {
  const [agentFilter, setAgentFilter] = useState<string>("");
  const [scopeFilter, setScopeFilter] = useState<"all" | "global" | "private">("all");
  const {
    learnings,
    hasMore,
    loadMore,
    isLoadingMore,
    isLoading,
    stats,
    updateLearning,
    isUpdating,
    updatingId,
    deleteLearning,
    isDeleting,
  } = useAgentLearnings(agentFilter || undefined, 20);

  const agentNames = stats ? Object.keys(stats.learningsByAgent) : [];

  const filteredLearnings =
    scopeFilter === "all" ? learnings : learnings.filter((l) => (scopeFilter === "global" ? l.isGlobal : !l.isGlobal));

  return (
    <Card padding="none" as="section">
      <CardHeader
        icon={<BrainCircuit />}
        title="Captured Learnings"
        description="Knowledge captured by agents during interactions"
        actions={stats && <Badge tone="neutral">{stats.totalLearnings} total</Badge>}
      />

      <div className={styles.filters}>
        <Select
          aria-label="Filter by agent"
          controlSize="sm"
          value={agentFilter}
          onChange={(e) => setAgentFilter(e.target.value)}
          wrapperClassName={styles.filter}
        >
          <option value="">All agents</option>
          {agentNames.map((name) => (
            <option key={name} value={name}>
              {name} ({stats?.learningsByAgent[name] ?? 0})
            </option>
          ))}
        </Select>
        <Select
          aria-label="Filter by scope"
          controlSize="sm"
          value={scopeFilter}
          onChange={(e) => setScopeFilter(e.target.value as "all" | "global" | "private")}
          wrapperClassName={styles.filter}
        >
          <option value="all">All scopes</option>
          <option value="global">Global only</option>
          <option value="private">Private only</option>
        </Select>
      </div>

      {isLoading && (
        <div className={styles.cardBody} aria-busy="true">
          <span className="visually-hidden">Loading learnings…</span>
          <SkeletonText lines={4} />
        </div>
      )}

      {!isLoading && filteredLearnings.length === 0 && (
        <EmptyState
          icon={<BrainCircuit />}
          title={scopeFilter !== "all" ? `No ${scopeFilter} learnings` : "No learnings captured yet"}
          hint={
            scopeFilter === "global"
              ? "Agents promote learnings to global only after validation. Captured patterns will appear here once approved."
              : scopeFilter === "private"
                ? "No private learnings for this filter. Private learnings are scoped to your agents only."
                : "Agents automatically capture learnings during analyses and store them in the vector knowledge base."
          }
        />
      )}

      {!isLoading && filteredLearnings.length > 0 && (
        <ul className={styles.learningList}>
          {filteredLearnings.map((l) => (
            <LearningCard
              key={l.id}
              learning={l}
              isSaving={isUpdating && updatingId === l.id}
              isDeleting={isDeleting}
              onSave={(content, tags) => updateLearning({ id: l.id, content, tags })}
              onDelete={() => deleteLearning(l.id)}
              formatDate={formatDate}
            />
          ))}
        </ul>
      )}

      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
    </Card>
  );
};
