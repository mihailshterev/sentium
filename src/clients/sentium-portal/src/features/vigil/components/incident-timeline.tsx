import { ShieldCheck } from "lucide-react";
import styles from "../vigil.module.css";
import type { Incident } from "@/features/vigil/types/serviceHealth";
import { EmptyState } from "@/shared/ui/empty-state";
import { formatRelativeTime } from "../../../shared/utils/formatters";
import { SeverityBadge } from "./severity-badge";

const formatDuration = (ms?: number | null) => {
  if (ms == null) {
    return null;
  }
  const totalSeconds = Math.round(ms / 1000);
  if (totalSeconds < 60) {
    return `${totalSeconds}s`;
  }
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  if (minutes < 60) {
    return `${minutes}m ${seconds}s`;
  }
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
};

export function IncidentTimeline({ incidents }: { incidents: Incident[] }) {
  if (incidents.length === 0) {
    return <EmptyState compact icon={<ShieldCheck />} title="No incidents - all targets stable" />;
  }

  return (
    <ol className={styles.incidents}>
      {incidents.map((incident) => {
        const duration = formatDuration(incident.durationMs);
        return (
          <li key={incident.id} className={styles.incident} data-state={incident.status}>
            <span className={styles.incidentMarker} aria-hidden="true" />
            <div className={styles.incidentBody}>
              <div className={styles.incidentTop}>
                <span className={styles.incidentTarget}>{incident.target}</span>
                <SeverityBadge severity={incident.severity} status={incident.status} />
              </div>
              <div className={styles.incidentMeta}>
                <span>Opened {formatRelativeTime(incident.openedAt)}</span>
                {incident.status === "Resolved" && incident.resolvedAt && (
                  <span>· Resolved {formatRelativeTime(incident.resolvedAt)}</span>
                )}
                {duration && <span>· {duration}</span>}
              </div>
              {incident.description && <p className={styles.incidentDesc}>{incident.description}</p>}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
