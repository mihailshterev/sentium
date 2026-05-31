import { Activity, ArrowRight } from "lucide-react";
import styles from "../dashboard.module.scss";
import EmptyState from "../../../components/ui/empty-state";
import type { WorkflowRun } from "../../../types/workflows";

interface WorkflowRunsFeedProps {
  runs: WorkflowRun[];
  loading: boolean;
  onNavigate: (to: string) => void;
}

const RISK_CLASS: Record<string, string> = {
  Low: styles.runRiskLow,
  Medium: styles.runRiskMedium,
  High: styles.runRiskHigh,
  Critical: styles.runRiskCritical,
};

function relativeTime(iso: string): string {
  const elapsed = Date.now() - new Date(iso).getTime();
  const seconds = Math.floor(elapsed / 1000);
  if (seconds < 60) {
    return "just now";
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes}m ago`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }
  return `${Math.floor(hours / 24)}d ago`;
}

const SkeletonRunRow = () => (
  <div className={styles.runRow}>
    <span className={`${styles.skeletonLine} ${styles.skeletonLineShort}`} style={{ marginTop: 2, flexShrink: 0 }} />
    <div className={styles.runBody}>
      <span className={`${styles.skeletonLine} ${styles.skeletonLineLong}`} />
      <span className={`${styles.skeletonLine} ${styles.skeletonLineShort}`} style={{ marginTop: 4 }} />
    </div>
  </div>
);

const WorkflowRunsFeed = ({ runs, loading, onNavigate }: WorkflowRunsFeedProps) => (
  <section className={styles.section}>
    <div className={styles.sectionHeader}>
      <span className={styles.sectionTitle}>Recent Workflow Runs</span>
      <span className={styles.sectionTag}>Live</span>
    </div>

    <div className={styles.runsFeed}>
      {loading ? (
        [0, 1, 2].map((i) => <SkeletonRunRow key={i} />)
      ) : runs.length === 0 ? (
        <EmptyState
          icon={<Activity size={20} />}
          title="No workflow runs yet"
          hint="Run a workflow to see results here"
        />
      ) : (
        runs.map((run) => (
          <div key={run.id} className={styles.runRow}>
            <span className={`${styles.runRiskBadge} ${RISK_CLASS[run.risk] ?? styles.runRiskLow}`}>{run.risk}</span>
            <div className={styles.runBody}>
              <p className={styles.runExplanation}>
                {run.explanation || run.recommendation || "Workflow run completed"}
              </p>
              <div className={styles.runMeta}>
                <span className={styles.runTime}>{relativeTime(run.startedAt)}</span>
                <button className={styles.runViewBtn} onClick={() => onNavigate(`/orchestration/runs/${run.id}`)}>
                  View <ArrowRight size={10} />
                </button>
              </div>
            </div>
          </div>
        ))
      )}
    </div>
  </section>
);

export default WorkflowRunsFeed;
