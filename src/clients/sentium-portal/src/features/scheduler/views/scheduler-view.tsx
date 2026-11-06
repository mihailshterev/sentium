import { Fragment, useState } from "react";
import { CalendarClock, ChevronDown, Info, RefreshCw, Trash2, Zap } from "lucide-react";
import { useDeleteJobMutation, useSchedulerJobs } from "@/features/scheduler/hooks/useScheduler";
import styles from "../scheduler.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Page } from "@/shared/ui/page";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";

type PendingDelete = { agentId: string; jobId: string } | null;

const COLUMNS = 7;

export function SchedulerView() {
  const { jobs, isLoading, refetch } = useSchedulerJobs();
  const deleteMutation = useDeleteJobMutation();
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<PendingDelete>(null);

  const toggleRow = (id: string) => setExpandedId((prev) => (prev === id ? null : id));

  const confirmDelete = async () => {
    if (!pendingDelete) {
      return;
    }
    setPendingDelete(null);
    try {
      await deleteMutation.mutateAsync(pendingDelete);
    } catch (err) {
      console.error("Failed to delete scheduled job from cluster context:", err);
    }
  };

  return (
    <Page
      title="Automated Scheduler"
      subtitle="Recurring jobs that agents run in the sandbox"
      actions={
        <Button
          variant="secondary"
          icon={<RefreshCw size={15} className={isLoading ? "animate-spin" : undefined} />}
          onClick={() => refetch()}
          disabled={isLoading}
        >
          Refresh engine
        </Button>
      }
    >
      <StatGrid minCellWidth={220}>
        <StatCard icon={<CalendarClock />} value={jobs.length} label="Active core loops" />
        <StatCard
          icon={<Zap />}
          value={deleteMutation.isPending ? "Syncing..." : "Connected"}
          label="Engine pipeline status"
          iconColor="green"
        />
      </StatGrid>

      <Card padding="none" as="section">
        <CardHeader
          title="Scheduled jobs"
          description={
            <span className={styles.notice}>
              <Info size={13} aria-hidden="true" />
              Deleting a job stops its cron loop on every application pod immediately.
            </span>
          }
        />

        <div className="data-table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Job name</th>
                <th>Agent ID</th>
                <th>Language</th>
                <th>Schedule</th>
                <th>Next run</th>
                <th>Status</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {isLoading && jobs.length === 0 && (
                <tr>
                  <td colSpan={COLUMNS} className={styles.emptyCell}>
                    Reading task signatures…
                  </td>
                </tr>
              )}

              {!isLoading && jobs.length === 0 && (
                <tr>
                  <td colSpan={COLUMNS} className={styles.emptyCell}>
                    No automated cron jobs detected.
                  </td>
                </tr>
              )}

              {jobs.map((job) => {
                const isExpanded = expandedId === job.jobId;
                return (
                  <Fragment key={job.jobId}>
                    <tr
                      className={styles.row}
                      data-expanded={isExpanded || undefined}
                      onClick={() => toggleRow(job.jobId)}
                      aria-expanded={isExpanded}
                    >
                      <td className={styles.jobName}>{job.jobName}</td>
                      <td className={styles.mono}>{job.agentId}</td>
                      <td>
                        <Badge tone="neutral">{job.language}</Badge>
                      </td>
                      <td>
                        <code className={styles.cron}>{job.cronExpression}</code>
                      </td>
                      <td>{job.nextRun ?? "Never / Paused"}</td>
                      <td>
                        <Badge tone="success" dot>
                          {job.status}
                        </Badge>
                      </td>
                      <td className={styles.actions}>
                        <Button
                          variant="ghost"
                          size="sm"
                          iconOnly
                          icon={<Trash2 size={15} />}
                          disabled={deleteMutation.isPending}
                          onClick={(e) => {
                            e.stopPropagation();
                            setPendingDelete({ agentId: job.agentId, jobId: job.jobId });
                          }}
                          title="Terminate Scheduled Task"
                          className={styles.deleteButton}
                        />
                        <ChevronDown size={15} className={isExpanded ? styles.chevronOpen : styles.chevron} />
                      </td>
                    </tr>

                    {isExpanded && (
                      <tr className={styles.detailRow}>
                        <td colSpan={COLUMNS}>
                          <dl className={styles.detailGrid}>
                            <div>
                              <dt>Composite job key</dt>
                              <dd className={styles.mono}>{job.jobId}</dd>
                            </div>
                            <div>
                              <dt>Last run</dt>
                              <dd>{job.previousRun ?? "Not run yet"}</dd>
                            </div>
                          </dl>
                          {job.codeSnippet && (
                            <div className={styles.codeWrap}>
                              <span className={styles.codeLabel}>Code</span>
                              <pre className={styles.code}>
                                <code>{job.codeSnippet}</code>
                              </pre>
                            </div>
                          )}
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>

      <ConfirmDialog
        open={pendingDelete !== null}
        variant="danger"
        title="Terminate scheduled job?"
        description={`Job "${pendingDelete?.jobId}" will be halted and removed from the cluster. All cron loops tied to it will stop immediately across all pods.`}
        confirmLabel="Terminate job"
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </Page>
  );
}
