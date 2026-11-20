import { Fragment, useState } from "react";
import { Link } from "react-router";
import {
  AlertTriangle,
  CalendarClock,
  ChevronDown,
  Cpu,
  Info,
  Pause,
  Pencil,
  Play,
  Plus,
  RefreshCw,
  Trash2,
} from "lucide-react";
import {
  useDeleteJobMutation,
  usePauseResumeMutation,
  useSchedulerJobs,
} from "@/features/scheduler/hooks/useScheduler";
import { useRole } from "@/shared/auth/hooks/useRole";
import styles from "../scheduler.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Page } from "@/shared/ui/page";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import { Tabs } from "@/shared/ui/tabs";
import { BackgroundJobsPanel } from "../components/background-jobs-panel";
import { ScheduleDialog } from "../components/schedule-dialog";
import type { CronJobRecord, ScheduleKind, ScheduleLastRun } from "../types/scheduler";
import { describeCadence } from "../utils/cadence";

type DialogState = { mode: "create" } | { mode: "edit"; job: CronJobRecord } | null;
type View = "schedules" | "background";

const COLUMNS = 7;

const KIND_LABEL: Record<ScheduleKind, string> = { Agent: "Agent", Workflow: "Workflow", Script: "Script" };
const KIND_TONE: Record<ScheduleKind, BadgeTone> = { Agent: "accent", Workflow: "purple", Script: "neutral" };

const RUN_TONE: Record<string, BadgeTone> = {
  Completed: "success",
  Failed: "danger",
  TimedOut: "warning",
  Interrupted: "warning",
  Cancelled: "neutral",
};

const RUN_LABEL: Record<string, string> = { TimedOut: "Timed out" };

const FAILED_STATUSES = new Set(["Failed", "TimedOut", "Interrupted"]);

export function SchedulerView() {
  const { isSovereign } = useRole();
  const { jobs, isLoading, refetch } = useSchedulerJobs();
  const deleteMutation = useDeleteJobMutation();
  const pauseResume = usePauseResumeMutation();
  const [view, setView] = useState<View>("schedules");
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<CronJobRecord | null>(null);
  const [dialog, setDialog] = useState<DialogState>(null);

  const toggleRow = (id: string) => setExpandedId((prev) => (prev === id ? null : id));
  const paused = jobs.filter((j) => j.paused).length;
  const failing = jobs.filter((j) => j.lastRun && FAILED_STATUSES.has(j.lastRun.status)).length;

  const confirmDelete = async () => {
    if (!pendingDelete) {
      return;
    }
    const { jobId } = pendingDelete;
    setPendingDelete(null);
    try {
      await deleteMutation.mutateAsync(jobId);
    } catch (err) {
      console.error("Failed to delete the schedule:", err);
    }
  };

  const showBackground = isSovereign && view === "background";

  return (
    <Page
      title="Scheduler"
      subtitle="Agent prompts, workflows and scripts that run on their own"
      actions={
        <>
          <Button
            variant="secondary"
            icon={<RefreshCw size={15} className={isLoading ? "animate-spin" : undefined} />}
            onClick={() => refetch()}
            disabled={isLoading}
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            icon={<Plus size={15} />}
            onClick={() => setDialog({ mode: "create" })}
            data-testid="schedule-new"
          >
            New schedule
          </Button>
        </>
      }
    >
      <StatGrid minCellWidth={200}>
        <StatCard icon={<CalendarClock />} value={jobs.length} label="Schedules" />
        <StatCard icon={<Pause />} value={paused} label="Paused" iconColor="amber" />
        <StatCard icon={<AlertTriangle />} value={failing} label="Last run failed" iconColor="red" />
      </StatGrid>

      {isSovereign && (
        <Tabs
          label="Scheduler sections"
          active={view}
          onChange={(id) => setView(id as View)}
          tabs={[
            { id: "schedules", label: "Schedules", icon: <CalendarClock size={14} />, count: jobs.length, testId: "scheduler-tab-schedules" },
            { id: "background", label: "Background jobs", icon: <Cpu size={14} />, testId: "scheduler-tab-background" },
          ]}
        />
      )}

      {showBackground ? (
        <BackgroundJobsPanel />
      ) : (
        <Card padding="none" as="section">
          <CardHeader
            title="Schedules"
            description={
              <span className={styles.notice}>
                <Info size={13} aria-hidden="true" />
                Each run appears in the run history and ends with a notification. Deleting a schedule removes it
                permanently.
              </span>
            }
          />

          <div className="data-table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Runs</th>
                  <th>When</th>
                  <th>Next run</th>
                  <th>Last run</th>
                  <th>Status</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {isLoading && jobs.length === 0 && (
                  <tr>
                    <td colSpan={COLUMNS} className={styles.emptyCell}>
                      Loading schedules…
                    </td>
                  </tr>
                )}

                {!isLoading && jobs.length === 0 && (
                  <tr>
                    <td colSpan={COLUMNS} className={styles.emptyCell}>
                      No schedules yet. Create one, or start from the morning briefing template.
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
                        data-testid="schedule-row"
                        onClick={() => toggleRow(job.jobId)}
                        aria-expanded={isExpanded}
                      >
                        <td>
                          <span className={styles.jobName}>{job.jobName}</span>
                          <Badge tone={KIND_TONE[job.kind] ?? "neutral"} className={styles.kindBadge}>
                            {KIND_LABEL[job.kind] ?? job.kind}
                          </Badge>
                        </td>
                        <td className={styles.mono}>{runsLabel(job)}</td>
                        <td>
                          <span title={job.cronExpression}>{describeCadence(job.cronExpression)}</span>
                          <span className={styles.zone}>{job.timeZone}</span>
                        </td>
                        <td>{job.paused ? "Paused" : (job.nextRun ?? "Never")}</td>
                        <td onClick={(e) => e.stopPropagation()}>
                          <LastRun lastRun={job.lastRun} previousRun={job.previousRun} />
                        </td>
                        <td>
                          <Badge tone={job.paused ? "warning" : "success"} dot>
                            {job.paused ? "Paused" : "Active"}
                          </Badge>
                        </td>
                        <td className={styles.actions}>
                          <Button
                            variant="ghost"
                            size="sm"
                            iconOnly
                            icon={job.paused ? <Play size={15} /> : <Pause size={15} />}
                            disabled={pauseResume.isPending}
                            title={job.paused ? "Resume schedule" : "Pause schedule"}
                            aria-label={job.paused ? `Resume ${job.jobName}` : `Pause ${job.jobName}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              pauseResume.mutate({ jobId: job.jobId, pause: !job.paused });
                            }}
                          />
                          <Button
                            variant="ghost"
                            size="sm"
                            iconOnly
                            icon={<Pencil size={15} />}
                            title="Edit schedule"
                            aria-label={`Edit ${job.jobName}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              setDialog({ mode: "edit", job });
                            }}
                          />
                          <Button
                            variant="ghost"
                            size="sm"
                            iconOnly
                            icon={<Trash2 size={15} />}
                            disabled={deleteMutation.isPending}
                            onClick={(e) => {
                              e.stopPropagation();
                              setPendingDelete(job);
                            }}
                            title="Delete schedule"
                            aria-label={`Delete ${job.jobName}`}
                            className={styles.deleteButton}
                          />
                          <ChevronDown size={15} className={isExpanded ? styles.chevronOpen : styles.chevron} />
                        </td>
                      </tr>

                      {isExpanded && (
                        <tr className={styles.detailRow}>
                          <td colSpan={COLUMNS}>
                            <ScheduleDetail job={job} />
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
      )}

      {dialog && (
        <ScheduleDialog
          key={dialog.mode === "edit" ? dialog.job.jobId : "new"}
          job={dialog.mode === "edit" ? dialog.job : undefined}
          onClose={() => setDialog(null)}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        variant="danger"
        title="Delete schedule?"
        description={`“${pendingDelete?.jobName}” will be removed and will not run again. Its past runs stay in the run history.`}
        confirmLabel="Delete schedule"
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </Page>
  );
}

function runsLabel(job: CronJobRecord) {
  switch (job.kind) {
    case "Agent":
      return job.agentId;
    case "Workflow":
      return job.targetName || "Workflow";
    default:
      return `${job.language} script · ${job.agentId}`;
  }
}

function LastRun({ lastRun, previousRun }: { lastRun: ScheduleLastRun | null; previousRun: string | null }) {
  if (!lastRun) {
    return <span className={styles.muted}>{previousRun ?? "Not run yet"}</span>;
  }

  const badge = (
    <Badge tone={RUN_TONE[lastRun.status] ?? "neutral"}>{RUN_LABEL[lastRun.status] ?? lastRun.status}</Badge>
  );
  const when = new Date(lastRun.at).toLocaleString(undefined, { dateStyle: "short", timeStyle: "short" });

  return (
    <span className={styles.lastRun}>
      {lastRun.runId ? (
        <Link to={`/orchestration/runs/${lastRun.runId}`} className={styles.runLink} data-testid="schedule-last-run">
          {badge}
        </Link>
      ) : (
        badge
      )}
      <span className={styles.muted}>{when}</span>
    </span>
  );
}

function ScheduleDetail({ job }: { job: CronJobRecord }) {
  const body = job.kind === "Script" ? job.codeSnippet : job.prompt;

  return (
    <>
      <dl className={styles.detailGrid}>
        <div>
          <dt>Cron expression</dt>
          <dd className={styles.mono}>
            {job.cronExpression} ({job.timeZone})
          </dd>
        </div>
        <div>
          <dt>Last fired</dt>
          <dd>{job.previousRun ?? "Not run yet"}</dd>
        </div>
        {job.kind !== "Script" && (
          <div>
            <dt>Pre-approved tools</dt>
            <dd>{job.preApprovedTools.length > 0 ? job.preApprovedTools.join(", ") : "None"}</dd>
          </div>
        )}
        <div>
          <dt>Schedule id</dt>
          <dd className={styles.mono}>{job.jobId}</dd>
        </div>
      </dl>
      {body && (
        <div className={styles.codeWrap}>
          <span className={styles.codeLabel}>{job.kind === "Script" ? "Code" : job.kind === "Agent" ? "Prompt" : "Input"}</span>
          <pre className={styles.code}>
            <code>{body}</code>
          </pre>
        </div>
      )}
    </>
  );
}
