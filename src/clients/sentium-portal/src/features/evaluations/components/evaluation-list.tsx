import { Link } from "react-router";
import { Gauge } from "lucide-react";
import styles from "../evaluations.module.css";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { SkeletonText } from "@/shared/ui/skeleton";
import { formatDateTimeShort } from "@/shared/utils/formatters";
import { useEvaluations } from "../hooks/useEvaluations";
import { EvaluationStatusBadge } from "./evaluation-status-badge";

export function EvaluationList() {
  const { evaluations, isLoading, hasMore, loadMore, isLoadingMore } = useEvaluations();

  return (
    <Card padding="none" as="section">
      <CardHeader icon={<Gauge />} title="Evaluations" description="Newest first. Open one to watch it or review its results." />
      {isLoading ? (
        <div className={styles.section}>
          <SkeletonText lines={3} />
        </div>
      ) : evaluations.length === 0 ? (
        <EmptyState icon={<Gauge />} title="No Evaluations yet" hint="Open a suite and choose Evaluate." compact />
      ) : (
        <ul className={styles.suiteList}>
          {evaluations.map((e) => (
            <li className={styles.suiteRow} key={e.id}>
              <Link className={styles.suiteMain} to={`/evaluations/${e.id}`}>
                <span className={styles.suiteTitleRow}>
                  <span className={styles.suiteName}>
                    {e.suiteName} v{e.suiteVersion}
                  </span>
                  <EvaluationStatusBadge status={e.status} />
                  {e.benchmarkId && <Badge tone="purple">benchmark</Badge>}
                  {e.completedCaseCount > 0 && (
                    <Badge tone={e.passedCaseCount === e.completedCaseCount ? "success" : "neutral"}>
                      {e.passedCaseCount}/{e.caseCount} passed
                    </Badge>
                  )}
                </span>
                <span className={styles.muted}>
                  {e.agentName} on {e.model} · {formatDateTimeShort(e.createdAt)}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
    </Card>
  );
}
