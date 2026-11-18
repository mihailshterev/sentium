import { Link } from "react-router";
import { Columns3 } from "lucide-react";
import styles from "../evaluations.module.css";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { SkeletonText } from "@/shared/ui/skeleton";
import { formatDateTimeShort } from "@/shared/utils/formatters";
import { useBenchmarks } from "../hooks/useBenchmarks";
import { isActive } from "../types/evaluations";

export function BenchmarkList() {
  const { benchmarks, isLoading, hasMore, loadMore, isLoadingMore } = useBenchmarks();

  return (
    <Card padding="none" as="section">
      <CardHeader icon={<Columns3 />} title="Benchmarks" description="Several models on the same suite version and agent." />
      {isLoading ? (
        <div className={styles.section}>
          <SkeletonText lines={2} />
        </div>
      ) : benchmarks.length === 0 ? (
        <EmptyState icon={<Columns3 />} title="No Benchmarks yet" hint="Open a suite and choose Benchmark." compact />
      ) : (
        <ul className={styles.suiteList}>
          {benchmarks.map((b) => {
            const active = b.evaluations.filter((e) => isActive(e.status)).length;
            return (
              <li className={styles.suiteRow} key={b.id}>
                <Link className={styles.suiteMain} to={`/evaluations/benchmarks/${b.id}`}>
                  <span className={styles.suiteTitleRow}>
                    <span className={styles.suiteName}>
                      {b.suiteName} v{b.suiteVersion}
                    </span>
                    <Badge tone="purple">{b.evaluations.length} models</Badge>
                    {active > 0 && (
                      <Badge tone="info" dot>
                        {active} to go
                      </Badge>
                    )}
                  </span>
                  <span className={styles.muted}>
                    {b.agentName} · {b.evaluations.map((e) => e.model).join(", ")} · {formatDateTimeShort(b.createdAt)}
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
    </Card>
  );
}
