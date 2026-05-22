import { Activity } from "lucide-react";
import styles from "../dashboard.module.scss";
import type { AgentRecord } from "../../../types/agents";
import type { WorkflowRecord } from "../../../types/workflows";

interface ActivityFeedProps {
  loading: boolean;
  agents: AgentRecord[];
  workflows: WorkflowRecord[];
}

const ActivityFeed = ({ loading, agents, workflows }: ActivityFeedProps) => (
  <div className={styles.activityFeed}>
    {loading ? (
      [0, 1, 2].map((i) => (
        <div key={i} className={styles.activityRow}>
          <div className={`${styles.activityDot} ${styles.skeletonDot}`} />
          <div className={styles.activityText}>
            <span className={`${styles.skeletonLine} ${styles.skeletonLineLong}`} />
            <span className={`${styles.skeletonLine} ${styles.skeletonLineShort}`} />
          </div>
        </div>
      ))
    ) : agents.length === 0 && workflows.length === 0 ? (
      <div className={styles.emptyState}>
        <Activity size={20} />
        <span>No recent activity</span>
      </div>
    ) : (
      <>
        {workflows.slice(0, 3).map((wf) => (
          <div key={wf.id} className={styles.activityRow}>
            <div className={`${styles.activityDot} ${styles.dotBlue}`} />
            <div className={styles.activityText}>
              <span className={styles.activityMain}>
                Workflow <strong>{wf.name}</strong> registered
              </span>
              <span className={styles.activityTime}>
                {wf.agents.length} agent{wf.agents.length !== 1 ? "s" : ""}
              </span>
            </div>
          </div>
        ))}
        {agents.slice(0, 3).map((ag) => (
          <div key={ag.id} className={styles.activityRow}>
            <div className={`${styles.activityDot} ${styles.dotGreen}`} />
            <div className={styles.activityText}>
              <span className={styles.activityMain}>
                Agent <strong>{ag.name}</strong> available
              </span>
              <span className={styles.activityTime}>{ag.model}</span>
            </div>
          </div>
        ))}
      </>
    )}
  </div>
);

export default ActivityFeed;
