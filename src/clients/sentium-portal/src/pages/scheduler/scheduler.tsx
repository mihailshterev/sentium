import { CalendarClock, RefreshCw, Trash2, Zap, Terminal, ChevronDown, ChevronUp, AlertTriangle } from "lucide-react";
import { useState } from "react";
import { useSchedulerJobs, useDeleteJobMutation } from "../../hooks/useScheduler";
import styles from "./scheduler.module.scss";
import PageHeader from "../../components/ui/page-header";

const Scheduler = () => {
  const { jobs, isLoading, refetch } = useSchedulerJobs();
  const deleteMutation = useDeleteJobMutation();
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const toggleRow = (id: string) => setExpandedId((prev) => (prev === id ? null : id));

  const handleDeleteJob = async (e: React.MouseEvent, agentId: string, jobId: string) => {
    e.stopPropagation();

    if (window.confirm(`Are you sure you want to terminate background job: ${jobId}?`)) {
      try {
        await deleteMutation.mutateAsync({ agentId, jobId });
      } catch (err) {
        console.error("Failed to delete scheduled job from cluster context:", err);
      }
    }
  };

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<CalendarClock size={18} className={styles.titleIcon} />}
        title="Automated Scheduler"
        subtitle="Quartz Cluster Core Orchestration — background crons running in sandbox environments"
        right={
          <div className={styles.headerActions}>
            <button className={styles.refreshBtn} onClick={() => refetch()} disabled={isLoading}>
              <RefreshCw size={13} className={isLoading ? styles.spinning : undefined} />
              Refresh Engine
            </button>
          </div>
        }
      />

      <div className={styles.body}>
        <div className={styles.statsRow}>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconBlue}`}>
              <CalendarClock size={16} />
            </div>
            <div>
              <span className={styles.statValue}>{jobs.length}</span>
              <span className={styles.statLabel}>Active Core Loops</span>
            </div>
          </div>
          <div className={styles.statCard}>
            <div className={`${styles.statIcon} ${styles.iconGreen}`}>
              <Zap size={16} />
            </div>
            <div>
              <span className={styles.statValue}>{deleteMutation.isPending ? "Syncing..." : "Connected"}</span>
              <span className={styles.statLabel}>Engine Pipeline Status</span>
            </div>
          </div>
        </div>

        <div className={styles.mainGrid}>
          <div className={styles.schedulerPanel}>
            <div className={styles.panelHeader}>
              <div className={styles.panelTitle}>
                <Terminal size={14} />
                Active Core Threads
              </div>
              <span className={styles.liveTag}>POLLING NATIVE</span>
            </div>

            <div className={styles.tableHead}>
              <span>Job Name</span>
              <span>Agent ID</span>
              <span>Language</span>
              <span>CRON Expression</span>
              <span>Next Execution</span>
              <span>Status</span>
              <span />
            </div>

            <div className={styles.tableBody}>
              {isLoading && jobs.length === 0 && (
                <div className={styles.emptyState}>Reading task signatures out of live cluster memory...</div>
              )}

              {!isLoading && jobs.length === 0 && (
                <div className={styles.emptyState}>No automated cron jobs detected inside the engine runtime.</div>
              )}

              {jobs.map((job) => {
                const isExpanded = expandedId === job.jobId;
                return (
                  <div key={job.jobId} className={styles.rowWrapper}>
                    <div
                      className={`${styles.tableRow} ${isExpanded ? styles.rowExpanded : ""}`}
                      onClick={() => toggleRow(job.jobId)}
                    >
                      <span className={styles.jobName}>{job.jobName}</span>
                      <span className={styles.agentId}>{job.agentId}</span>
                      <span className={styles.languageBadge}>{job.language}</span>
                      <span className={styles.cronText}>
                        <code>{job.cronExpression}</code>
                      </span>
                      <span className={styles.timeText}>{job.nextRun ?? "Never / Paused"}</span>
                      <span>
                        <span className={`${styles.statusBadge} ${styles.statusNormal}`}>{job.status}</span>
                      </span>
                      <div className={styles.actionsCell}>
                        <button
                          className={styles.deleteBtn}
                          disabled={deleteMutation.isPending}
                          onClick={(e) => handleDeleteJob(e, job.agentId, job.jobId)}
                          title="Terminate Scheduled Task"
                        >
                          <Trash2 size={13} />
                        </button>
                        {isExpanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                      </div>
                    </div>

                    {isExpanded && (
                      <div className={styles.jobDetail}>
                        <div className={styles.detailGrid}>
                          <div>
                            <span className={styles.detailLabel}>Composite Job Key</span>
                            <span className={`${styles.detailValue} ${styles.mono}`}>{job.jobId}</span>
                          </div>
                          <div>
                            <span className={styles.detailLabel}>Last Checked Fire Time</span>
                            <span className={styles.detailValue}>
                              {job.previousRun ?? "No execution records logs saved yet."}
                            </span>
                          </div>
                        </div>
                        {job.codeSnippet && (
                          <div>
                            <span className={styles.detailLabel}>Target Isolated Executable Shell Payload</span>
                            <pre className={styles.codeBlock}>
                              <code>{job.codeSnippet}</code>
                            </pre>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          <div className={styles.rightCol}>
            <div className={styles.card}>
              <div className={styles.cardHeader}>
                <div className={styles.panelTitle}>
                  <AlertTriangle size={14} />
                  Engine Notice
                </div>
              </div>
              <div className={styles.cardBody}>
                <p className={styles.guideText}>
                  These values are bound to your active cluster context. Purging a job index here explicitly halts
                  operational cron loops across all application pods immediately.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Scheduler;
