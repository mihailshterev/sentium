import { useState } from "react";
import { Bot, BrainCircuit, Database, FlaskConical, Loader, RefreshCw, Trash2, X } from "lucide-react";
import styles from "../knowledge-base.module.scss";
import { useAgentLearnings } from "../../../hooks/useAgentLearnings";
import { useKnowledgeBaseStats } from "../../../hooks/useKnowledgeBaseStats";
import ConfirmDialog from "../../../components/ui/confirm-dialog";
import EmptyState from "../../../components/ui/empty-state";
import LoadMore from "../../../components/ui/load-more";
import LearningCard from "./learning-card";

const formatDate = (iso: string) => {
  const d = new Date(iso);
  return (
    d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) +
    " " +
    d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
  );
};

export const GlobalContextTab = () => {
  const { collections, isLoading, error, refetch, deleteCollection, isDeleting } = useKnowledgeBaseStats();
  const { stats, isStatsLoading } = useAgentLearnings();
  const [pendingDelete, setPendingDelete] = useState<string | null>(null);

  const totalVectors = collections.reduce((sum, c) => sum + c.pointCount, 0);

  return (
    <>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div className={styles.cardHeaderLeft}>
            <FlaskConical size={15} className={styles.cardIconCyan} />
            <div>
              <p className={styles.cardTitle}>Knowledge Base Overview</p>
              <p className={styles.cardSubtitle}>Live statistics from the Qdrant vector store</p>
            </div>
          </div>
          <button className={styles.btnRefresh} onClick={() => void refetch()} disabled={isLoading}>
            <RefreshCw size={13} />
            Refresh
          </button>
        </div>

        <div className={styles.cardBody}>
          {isLoading && (
            <p className={styles.loadingText}>
              <Loader size={14} className="animate-spin" />
              Querying vector store…
            </p>
          )}

          {error && (
            <div className={styles.alertError}>
              <X size={14} />
              {error.message}
            </div>
          )}

          {!isLoading && !error && (
            <>
              <div className={styles.statsGrid}>
                <div className={styles.statCard}>
                  <span className={styles.statValue}>{collections.length}</span>
                  <span className={styles.statLabel}>Collections</span>
                </div>
                <div className={styles.statCard}>
                  <span className={styles.statValue}>{totalVectors.toLocaleString()}</span>
                  <span className={styles.statLabel}>Total Vectors</span>
                </div>
                {!isStatsLoading && stats && (
                  <>
                    <div className={styles.statCard}>
                      <span className={styles.statValue}>{stats.totalLearnings}</span>
                      <span className={styles.statLabel}>Agent Learnings</span>
                    </div>
                    <div className={styles.statCard}>
                      <span className={`${styles.statValue} ${styles.statValueCyan}`}>{stats.globalLearnings}</span>
                      <span className={styles.statLabel}>Global (Shared)</span>
                    </div>
                    <div className={styles.statCard}>
                      <span className={styles.statValue}>{stats.pendingIngestion}</span>
                      <span className={styles.statLabel}>Pending Ingestion</span>
                    </div>
                  </>
                )}
              </div>

              {collections.length > 0 ? (
                <div className={styles.collectionList}>
                  {collections.map((c) => (
                    <div key={c.collectionName} className={styles.collectionRow}>
                      <span className={styles.collectionName}>{c.collectionName}</span>
                      <div className={styles.collectionMeta}>
                        <span className={styles.metaChip}>{c.pointCount.toLocaleString()} vectors</span>
                        <span className={styles.metaChip}>{c.vectorSize}‑dim</span>
                        <span className={styles.metaChip}>{c.distanceMetric}</span>
                        <span className={c.pointCount > 0 ? styles.pillGreen : styles.pillAmber}>
                          {c.pointCount > 0 ? "Active" : "Empty"}
                        </span>
                        <button
                          className={styles.deleteBtn}
                          onClick={() => setPendingDelete(c.collectionName)}
                          disabled={isDeleting}
                          title="Delete Collection"
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <EmptyState
                  icon={<Database size={32} />}
                  title="No collections found"
                  hint="Collections are created automatically when documents are first ingested."
                />
              )}
            </>
          )}
        </div>
      </div>

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
        <div className={styles.card}>
          <div className={styles.cardHeader}>
            <div className={styles.cardHeaderLeft}>
              <Bot size={15} className={styles.cardIconGreen} />
              <div>
                <p className={styles.cardTitle}>Learnings by Agent</p>
                <p className={styles.cardSubtitle}>Insights captured per agent role</p>
              </div>
            </div>
          </div>
          <div className={styles.cardBody}>
            <div className={styles.statsGrid}>
              {Object.entries(stats.learningsByAgent).map(([agent, count]) => (
                <div key={agent} className={styles.statCard}>
                  <span className={styles.statValue}>{count}</span>
                  <span className={styles.statLabel}>{agent}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
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
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <BrainCircuit size={15} className={styles.cardIconPurple} />
          <div>
            <p className={styles.cardTitle}>Captured Learnings</p>
            <p className={styles.cardSubtitle}>Knowledge captured by agents during interactions</p>
          </div>
        </div>
        {stats && <span className={styles.pillPurple}>{stats.totalLearnings} total</span>}
      </div>

      <div className={styles.cardBody}>
        <div className={styles.filterRow}>
          <select className={styles.filterSelect} value={agentFilter} onChange={(e) => setAgentFilter(e.target.value)}>
            <option value="">All agents</option>
            {agentNames.map((name) => (
              <option key={name} value={name}>
                {name} ({stats?.learningsByAgent[name] ?? 0})
              </option>
            ))}
          </select>
          <select
            className={styles.filterSelect}
            value={scopeFilter}
            onChange={(e) => setScopeFilter(e.target.value as "all" | "global" | "private")}
          >
            <option value="all">All scopes</option>
            <option value="global">Global only</option>
            <option value="private">Private only</option>
          </select>
        </div>

        {isLoading && (
          <p className={styles.loadingText}>
            <Loader size={14} className="animate-spin" />
            Loading learnings…
          </p>
        )}

        {!isLoading && filteredLearnings.length === 0 && (
          <EmptyState
            icon={<BrainCircuit size={32} />}
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
          <div className={styles.learningList}>
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
          </div>
        )}

        <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
      </div>
    </div>
  );
};
