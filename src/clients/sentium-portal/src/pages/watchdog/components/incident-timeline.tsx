import { ShieldCheck } from "lucide-react";
import styles from "../watchdog.module.scss";
import type { Incident } from "../../../types/serviceHealth";
import { formatRelativeTime } from "../../../utils/formatters";
import SeverityBadge from "./severity-badge";

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

const IncidentTimeline = ({ incidents }: { incidents: Incident[] }) => {
  if (incidents.length === 0) {
    return (
      <div className={styles.incidentEmpty}>
        <ShieldCheck size={20} />
        <span>No incidents - all targets stable</span>
      </div>
    );
  }

  return (
    <div className={styles.incidentList}>
      {incidents.map((incident) => (
        <div key={incident.id} className={styles.incidentRow}>
          <span
            className={`${styles.incidentMarker} ${incident.status === "Resolved" ? styles.incidentMarkerResolved : styles.incidentMarkerOpen}`}
          />
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
              {formatDuration(incident.durationMs) && <span>· {formatDuration(incident.durationMs)}</span>}
            </div>
            {incident.description && <div className={styles.incidentDesc}>{incident.description}</div>}
          </div>
        </div>
      ))}
    </div>
  );
};

export default IncidentTimeline;
