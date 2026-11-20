import { Activity, Server } from "lucide-react";
import styles from "../scheduler.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { StatusMessage } from "@/shared/ui/status-message";
import { useBackgroundJobs } from "../hooks/useScheduler";
import type { BackgroundServiceState } from "../types/scheduler";

const STATE_TONE: Record<BackgroundServiceState, BadgeTone> = {
  Running: "success",
  Hosted: "info",
  NotStarted: "neutral",
  Stopped: "warning",
  Faulted: "danger",
};

const STATE_LABEL: Record<BackgroundServiceState, string> = {
  Running: "Running",
  Hosted: "Hosted",
  NotStarted: "Not started",
  Stopped: "Stopped",
  Faulted: "Faulted",
};

/** Every worker hosted in Sentium.Web and the work in progress right now, across all users (Sovereign only). */
export function BackgroundJobsPanel() {
  const { snapshot, isLoading, error } = useBackgroundJobs(true);

  if (error) {
    return <StatusMessage variant="error" message="The background jobs could not be loaded." />;
  }
  if (isLoading || !snapshot) {
    return <StatusMessage variant="loading" message="Reading background jobs…" />;
  }

  const faulted = snapshot.services.filter((s) => s.state === "Faulted").length;

  return (
    <>
      <Card padding="none" as="section" data-testid="background-running">
        <CardHeader
          icon={<Activity size={16} />}
          title="Running now"
          description="Workflow runs (scheduled or not) and scheduled jobs in progress. Refreshes every few seconds."
        />
        {snapshot.running.length === 0 ? (
          <p className={styles.emptyCell}>Nothing is running in the background right now.</p>
        ) : (
          <div className="data-table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Work</th>
                  <th>Kind</th>
                  <th>Detail</th>
                  <th>Started</th>
                </tr>
              </thead>
              <tbody>
                {snapshot.running.map((a, i) => (
                  <tr key={`${a.source}-${a.name}-${a.detail ?? i}`}>
                    <td className={styles.jobName}>{a.name}</td>
                    <td>
                      <Badge tone="neutral">{a.source}</Badge>
                    </td>
                    <td className={styles.mono}>{a.detail ?? "—"}</td>
                    <td>{a.startedAt ? formatTime(a.startedAt) : "Waiting"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Card padding="none" as="section" data-testid="background-services">
        <CardHeader
          icon={<Server size={16} />}
          title="Background services"
          description={
            faulted > 0
              ? `${faulted} service${faulted === 1 ? "" : "s"} stopped with an error and will not recover until Sentium restarts.`
              : "The long-running workers inside Sentium. Hosted services start and stop with the app and have no loop of their own."
          }
        />
        <div className="data-table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Service</th>
                <th>Module</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              {snapshot.services.map((s) => (
                <tr key={s.type}>
                  <td>
                    <span className={styles.jobName}>{s.name}</span>
                    <span className={styles.serviceType}>{s.type}</span>
                    {s.error && <span className={styles.serviceError}>{s.error}</span>}
                  </td>
                  <td>{s.module}</td>
                  <td>
                    <Badge tone={STATE_TONE[s.state] ?? "neutral"} dot>
                      {STATE_LABEL[s.state] ?? s.state}
                    </Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </>
  );
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "short", timeStyle: "medium" });
}
