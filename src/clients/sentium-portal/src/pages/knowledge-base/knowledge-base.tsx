import { useState } from "react";
import { Bot, BrainCircuit, Database, FlaskConical, Loader, Pencil, RefreshCw, Trash2, X, Check } from "lucide-react";
import styles from "./knowledge-base.module.scss";
import { useAgentLearnings } from "../../hooks/useAgentLearnings";
import { useKnowledgeBaseStats } from "../../hooks/useKnowledgeBaseStats";

type Tab = "context" | "learnings";

const KnowledgeBase = () => {
  const [activeTab, setActiveTab] = useState<Tab>("context");

  return (
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Database size={18} className={styles.titleIcon} />
          <div>
            <h1 className={styles.pageTitle}>Knowledge Base</h1>
            <p className={styles.pageSubtitle}>Global agent context, vector store statistics, and captured learnings</p>
          </div>
        </div>
      </div>

      <div className={styles.tabs}>
        <button
          className={`${styles.tab} ${activeTab === "context" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("context")}
        >
          <FlaskConical size={14} />
          Global Context
        </button>
        <button
          className={`${styles.tab} ${activeTab === "learnings" ? styles.activeTab : ""}`}
          onClick={() => setActiveTab("learnings")}
        >
          <BrainCircuit size={14} />
          Agent Learnings
        </button>
      </div>

      <div className={styles.body}>
        {activeTab === "context" && <GlobalContextTab />}
        {activeTab === "learnings" && <AgentLearningsTab />}
      </div>
    </div>
  );
};

const GlobalContextTab = () => {
  const { collections, isLoading, error, refetch } = useKnowledgeBaseStats();
  const { stats, isStatsLoading } = useAgentLearnings();

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
          {isLoading && <p className={styles.loadingText}>Querying vector store…</p>}

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
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className={styles.emptyState}>
                  <Database size={32} className={styles.emptyIcon} />
                  <p className={styles.emptyTitle}>No collections found</p>
                  <p className={styles.emptyDesc}>
                    Collections are created automatically when documents are first ingested.
                  </p>
                </div>
              )}
            </>
          )}
        </div>
      </div>

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

const AgentLearningsTab = () => {
  const [agentFilter, setAgentFilter] = useState<string>("");
  const { learnings, isLoading, stats, updateLearning, isUpdating, updatingId, deleteLearning, isDeleting } =
    useAgentLearnings(agentFilter || undefined, 100);

  const agentNames = stats ? Object.keys(stats.learningsByAgent) : [];

  const formatDate = (iso: string) => {
    const d = new Date(iso);
    return (
      d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) +
      " " +
      d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })
    );
  };

  return (
    <div className={styles.card}>
      <div className={styles.cardHeader}>
        <div className={styles.cardHeaderLeft}>
          <BrainCircuit size={15} className={styles.cardIconPurple} />
          <div>
            <p className={styles.cardTitle}>Captured Learnings</p>
            <p className={styles.cardSubtitle}>
              Knowledge captured by agents during interactions — auto-ingested into the vector knowledge base
            </p>
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
        </div>

        {isLoading && <p className={styles.loadingText}>Loading learnings…</p>}

        {!isLoading && learnings.length === 0 && (
          <div className={styles.emptyState}>
            <BrainCircuit size={32} className={styles.emptyIcon} />
            <p className={styles.emptyTitle}>No learnings captured yet</p>
            <p className={styles.emptyDesc}>
              Agents automatically capture learnings during analyses and store them in the vector knowledge base.
            </p>
          </div>
        )}

        {!isLoading && learnings.length > 0 && (
          <div className={styles.learningList}>
            {learnings.map((l) => (
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
      </div>
    </div>
  );
};

interface LearningCardProps {
  learning: {
    id: string;
    agentName: string;
    content: string;
    tags: string;
    capturedAt: string;
    isIngested: boolean;
  };
  isSaving: boolean;
  isDeleting: boolean;
  onSave: (content: string, tags: string) => void;
  onDelete: () => void;
  formatDate: (iso: string) => string;
}

const LearningCard = ({ learning: l, isSaving, isDeleting, onSave, onDelete, formatDate }: LearningCardProps) => {
  const [editing, setEditing] = useState(false);
  const [editContent, setEditContent] = useState(l.content);
  const [editTags, setEditTags] = useState(l.tags);

  const handleEdit = () => {
    setEditContent(l.content);
    setEditTags(l.tags);
    setEditing(true);
  };

  const handleCancel = () => setEditing(false);

  const handleSave = () => {
    if (!editContent.trim()) return;
    onSave(editContent.trim(), editTags.trim());
    setEditing(false);
  };

  return (
    <div className={`${styles.learningCard} ${editing ? styles.learningCardEditing : ""}`}>
      <div className={styles.learningCardHeader}>
        <div className={styles.learningMeta}>
          <span className={styles.agentBadge}>{l.agentName}</span>
          <span className={styles.learningDate}>{formatDate(l.capturedAt)}</span>
          {l.tags &&
            l.tags
              .split(",")
              .filter(Boolean)
              .map((tag) => (
                <span key={tag} className={styles.tag}>
                  {tag.trim()}
                </span>
              ))}
          <span className={l.isIngested ? styles.pillGreen : styles.pillAmber}>
            {l.isIngested ? "Indexed" : "Pending"}
          </span>
        </div>

        <div className={styles.cardActions}>
          {editing ? (
            <button className={`${styles.btnIcon} ${styles.btnIconActive}`} onClick={handleCancel} title="Cancel">
              <X size={13} />
            </button>
          ) : (
            <button className={styles.btnIcon} onClick={handleEdit} title="Edit learning" disabled={isDeleting}>
              <Pencil size={13} />
            </button>
          )}
          <button
            className={`${styles.btnIcon} ${styles.btnIconDanger}`}
            disabled={isDeleting || isSaving}
            onClick={onDelete}
            title="Delete learning"
          >
            <Trash2 size={13} />
          </button>
        </div>
      </div>

      {editing ? (
        <div className={styles.editBody}>
          <p className={styles.editLabel}>Content (markdown)</p>
          <textarea
            className={styles.editTextarea}
            value={editContent}
            onChange={(e) => setEditContent(e.target.value)}
            spellCheck={false}
          />
          <p className={styles.editLabel}>Tags (comma-separated)</p>
          <input
            className={styles.editTagsInput}
            type="text"
            value={editTags}
            onChange={(e) => setEditTags(e.target.value)}
            placeholder="e.g. workflow, memory, agent"
          />
          <div className={styles.editActions}>
            <button className={styles.btnSecondary} onClick={handleCancel}>
              Cancel
            </button>
            <button className={styles.btnPrimary} onClick={handleSave} disabled={isSaving || !editContent.trim()}>
              {isSaving ? <Loader size={12} /> : <Check size={12} />}
              {isSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      ) : (
        <div className={styles.learningContent}>{l.content}</div>
      )}
    </div>
  );
};

export default KnowledgeBase;
