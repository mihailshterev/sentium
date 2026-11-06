import { Activity, ArrowRight } from "lucide-react";
import styles from "../dashboard.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Skeleton } from "@/shared/ui/skeleton";
import type { WorkflowRun } from "@/features/workflows/types/workflows";

interface WorkflowRunsFeedProps {
  runs: WorkflowRun[];
  loading: boolean;
  onNavigate: (to: string) => void;
}

const RISK_TONE: Record<string, BadgeTone> = {
  Low: "success",
  Medium: "warning",
  High: "danger",
  Critical: "danger",
};

function relativeTime(iso: string): string {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
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
  <li className={styles.runRow}>
    <Skeleton width={56} height={20} radius="full" />
    <Skeleton width="55%" />
    <Skeleton width={48} className={styles.runSkeletonTime} />
  </li>
);

export function WorkflowRunsFeed({ runs, loading, onNavigate }: WorkflowRunsFeedProps) {
  return (
    <Card padding="none" as="section" className={styles.runsCard}>
      <CardHeader
        title="Recent Workflow Runs"
        description="Latest orchestration results"
        actions={
          <Badge tone="success" dot>
            Live
          </Badge>
        }
      />

      {loading ? (
        <ul className={styles.runList} aria-busy="true">
          {[0, 1, 2, 3].map((i) => (
            <SkeletonRunRow key={i} />
          ))}
        </ul>
      ) : runs.length === 0 ? (
        <EmptyState icon={<Activity />} title="No workflow runs yet" hint="Run a workflow to see results here" />
      ) : (
        <ul className={styles.runList}>
          {runs.map((run) => (
            <li key={run.id} className={styles.runRow}>
              <Badge tone={RISK_TONE[run.risk] ?? "neutral"} className={styles.runRisk}>
                {run.risk}
              </Badge>
              <p className={styles.runText}>{run.explanation || run.recommendation || "Workflow run completed"}</p>
              <time className={styles.runTime} dateTime={run.startedAt}>
                {relativeTime(run.startedAt)}
              </time>
              <Button
                variant="ghost"
                size="sm"
                iconRight={<ArrowRight size={14} />}
                onClick={() => onNavigate(`/orchestration/runs/${run.id}`)}
              >
                View
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
