import { useState } from "react";
import { Activity, Code2, File, RefreshCw, ShieldOff, Terminal, XCircle } from "lucide-react";
import styles from "./sandbox.module.scss";
import { useSandboxExecutions } from "../../hooks/useSandboxExecutions";
import { formatTimeHms } from "../../utils/formatters";
import PageHeader from "../../components/ui/page-header";
import StatCard from "../../components/ui/stat-card";
import EmptyState from "../../components/ui/empty-state";
import ArtifactCard from "./components/artifact-card";
import MetadataBadges from "./components/metadata-badges";
import TerminalOutput from "./components/terminal-output";
import AuditContext from "./components/audit-context";

const Sandbox = () => {
  const { executions, isLoading, refetch } = useSandboxExecutions(100);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const selected = selectedId ? (executions.find((e) => e.jobId === selectedId) ?? null) : (executions[0] ?? null);

  const total = executions.length;
  const succeeded = executions.filter((e) => e.succeeded).length;
  const failed = executions.filter((e) => !e.succeeded && !e.policyDenied).length;
  const denied = executions.filter((e) => e.policyDenied).length;

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
            <button className={styles.refreshBtn} onClick={() => refetch()} disabled={isLoading}>
              <RefreshCw size={13} className={isLoading ? styles.spinning : undefined} />
              Refresh
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <div className={styles.statsRow}>
          <StatCard icon={<Activity size={16} />} value={total} label="Total Runs" iconColor="blue" />
          <StatCard icon={<Activity size={16} />} value={succeeded} label="Succeeded" iconColor="green" />
          <StatCard icon={<XCircle size={16} />} value={failed} label="Failed" iconColor="red" />
          <StatCard icon={<ShieldOff size={16} />} value={denied} label="Policy Denied" iconColor="amber" />
        </div>

        <div className={styles.mainGrid}>
          <div className={styles.leftCol}>
            <div className={styles.card}>
              <div className={styles.panelHeader}>
                <span className={styles.panelTitle}>
                  <Activity size={14} /> Execution History
                </span>
              </div>
              <div className={styles.historyList}>
                {executions.length === 0 && !isLoading && (
                  <p className={styles.historyEmpty}>
                    No executions recorded yet. They appear here as agents run code.
                  </p>
                )}
                {executions.map((e) => {
                  const isActive = selected?.jobId === e.jobId;
                  const statusCls = e.policyDenied
                    ? styles.statusDenied
                    : e.succeeded
                      ? styles.statusOk
                      : styles.statusFail;
                  const statusLabel = e.policyDenied ? "Denied" : e.succeeded ? "OK" : "Fail";
                  return (
                    <div
                      key={e.jobId}
                      className={`${styles.historyRow} ${isActive ? styles.historyRowActive : ""}`}
                      onClick={() => setSelectedId(e.jobId)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(ev) => ev.key === "Enter" && setSelectedId(e.jobId)}
                    >
                      <span className={styles.historyTime}>{formatTimeHms(e.executedAt)}</span>
                      <span className={styles.historyAgent}>{e.agentId}</span>
                      <span
                        className={`${styles.langChip} ${e.language === "Python" ? styles.langPython : styles.langNode}`}
                      >
                        {e.language}
                      </span>
                      <span className={`${styles.statusChip} ${statusCls}`}>{statusLabel}</span>
                    </div>
                  );
                })}
              </div>
            </div>

            {selected && (
              <div className={styles.card}>
                <div className={styles.panelHeader}>
                  <span className={styles.panelTitle}>
                    <Code2 size={14} /> Audit Context
                  </span>
                </div>
                <AuditContext entry={selected} />
              </div>
            )}
          </div>

          <div className={styles.rightCol}>
            {!selected ? (
              <EmptyState
                icon={<Terminal size={38} />}
                title="Select an execution from the history to inspect its output"
              />
            ) : (
              <>
                <div className={styles.card}>
                  <MetadataBadges entry={selected} />
                  <TerminalOutput entry={selected} />
                </div>

                {selected.artifacts.length > 0 && (
                  <div className={styles.card}>
                    <div className={styles.panelHeader}>
                      <span className={styles.panelTitle}>
                        <File size={14} /> Artifacts
                      </span>
                      <span style={{ fontSize: "0.72rem", color: "var(--text-muted)" }}>
                        {selected.artifacts.length} file{selected.artifacts.length !== 1 ? "s" : ""}
                      </span>
                    </div>
                    <div className={styles.artifactsBody}>
                      <div className={styles.artifactsGrid}>
                        {selected.artifacts.map((a) => (
                          <ArtifactCard key={a.blobUri} artifact={a} />
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Sandbox;
