import { useId, useState } from "react";
import { Cpu } from "lucide-react";
import styles from "./inference.module.css";
import { useInferenceOverview } from "@/features/vigil/hooks/useInferenceOverview";
import {
  INFERENCE_RANGES,
  type InferenceOverview,
  type InferenceRange,
  type InferenceServer,
} from "@/features/vigil/types/inference";
import { formatCount, formatSeconds, LOCALE } from "@/features/vigil/utils/inference-format";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { Skeleton } from "@/shared/ui/skeleton";
import { Tabs, type TabItem } from "@/shared/ui/tabs";
import { ModelSeries } from "./model-series";

const DEFAULT_RANGE: InferenceRange = "24h";
const MS_PER_MINUTE = 60_000;

const RANGE_TABS: TabItem[] = INFERENCE_RANGES.map((range) => ({ id: range, label: range, testId: `inference-range-${range}` }));

const CALLER_LABELS: Record<string, string> = {
  Agents: "Agents",
  Direct: "Prompt tools",
  IntentCheck: "Sentinel intent checks",
  Embeddings: "Embeddings",
  Other: "Other",
};

/** Sovereign-only inference panel: live model states, per-model throughput and load, usage per caller, model loads. */
export function InferencePanel() {
  const [range, setRange] = useState<InferenceRange>(DEFAULT_RANGE);
  const { overview, isLoading, isUpdating, error } = useInferenceOverview(range);

  return (
    <Card padding="none" as="section" data-testid="vigil-inference">
      <CardHeader
        icon={<Cpu />}
        title="Inference"
        description="Throughput and load per model, and token usage per caller"
        actions={
          <Tabs
            tabs={RANGE_TABS}
            active={range}
            onChange={(id) => setRange(id as InferenceRange)}
            label="Time range"
          />
        }
      />

      <div className={styles.body} data-updating={isUpdating || undefined} aria-busy={isUpdating}>
        {isLoading ? (
          <div className={styles.loading}>
            <Skeleton width="40%" />
            <Skeleton height={120} />
          </div>
        ) : error || !overview ? (
          <p className={styles.empty}>Inference metrics are unavailable right now.</p>
        ) : (
          <OverviewBody overview={overview} />
        )}
      </div>
    </Card>
  );
}

function OverviewBody({ overview }: { overview: InferenceOverview }) {
  const from = Date.parse(overview.from);
  const to = Date.parse(overview.to);
  const stepMs = overview.stepMinutes * MS_PER_MINUTE;
  const models = modelKeys(overview);

  return (
    <>
      {models.length === 0 ? (
        <p className={styles.empty}>No model has reported yet. Metrics appear once an inference server answers.</p>
      ) : (
        <div className={styles.models}>
          {models.map(({ server, model }) => (
            <ModelSeries
              key={`${server}:${model}`}
              server={server}
              model={model}
              state={overview.models.find((m) => m.server === server && m.model === model)}
              points={overview.series.find((s) => s.server === server && s.model === model)?.points ?? []}
              from={from}
              to={to}
              stepMs={stepMs}
            />
          ))}
        </div>
      )}

      <div className={styles.split}>
        <UsageTable overview={overview} />
        <ModelLoads overview={overview} />
      </div>
    </>
  );
}

function UsageTable({ overview }: { overview: InferenceOverview }) {
  const captionId = useId();

  return (
    <div className={styles.block}>
      <h3 id={captionId} className={styles.blockTitle}>
        Usage by caller
      </h3>
      {overview.usage.length === 0 ? (
        <p className={styles.empty}>No requests in this range.</p>
      ) : (
        <div className={`data-table-scroll ${styles.tableScroll}`} tabIndex={0} role="region" aria-labelledby={captionId}>
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Caller</th>
                <th scope="col">Model</th>
                <th scope="col">Requests</th>
                <th scope="col">Errors</th>
                <th scope="col">Tokens in</th>
                <th scope="col">Tokens out</th>
                <th scope="col">Avg duration</th>
                <th scope="col">Avg first token</th>
              </tr>
            </thead>
            <tbody>
              {overview.usage.map((u) => (
                <tr key={`${u.caller}:${u.model}:${u.operation}`}>
                  <th scope="row" className={styles.rowHeader}>
                    {CALLER_LABELS[u.caller] ?? u.caller}
                  </th>
                  <td>
                    {u.model}
                    <span className={styles.operation}>{u.operation}</span>
                  </td>
                  <td className={styles.num}>{formatCount(u.requests)}</td>
                  <td className={styles.num} data-errors={u.errors > 0 || undefined}>
                    {formatCount(u.errors)}
                  </td>
                  <td className={styles.num}>{formatCount(u.inputTokens)}</td>
                  <td className={styles.num}>{formatCount(u.outputTokens)}</td>
                  <td className={styles.num}>{formatSeconds(u.averageDurationSeconds)}</td>
                  <td className={styles.num}>{formatSeconds(u.averageTimeToFirstTokenSeconds)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ModelLoads({ overview }: { overview: InferenceOverview }) {
  const headingId = useId();
  const time = new Intl.DateTimeFormat(LOCALE, { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

  return (
    <section className={styles.block} aria-labelledby={headingId}>
      <h3 id={headingId} className={styles.blockTitle}>
        Model loads
      </h3>
      {overview.loads.length === 0 ? (
        <p className={styles.empty}>No model loads seen in this range.</p>
      ) : (
        <ul className={styles.loads}>
          {overview.loads.map((load) => (
            <li key={`${load.server}:${load.model}:${load.completedAt}`} className={styles.load}>
              <span className={styles.loadModel}>{load.model}</span>
              <time dateTime={load.completedAt} className={styles.loadTime}>
                {time.format(Date.parse(load.completedAt))}
              </time>
              <span className={styles.num}>{formatSeconds(load.durationSeconds)}</span>
              <Badge tone={load.succeeded ? "success" : "danger"}>{load.succeeded ? "loaded" : "failed"}</Badge>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** Every model with live state or stored samples, chat models first, each once. */
function modelKeys(overview: InferenceOverview): { server: InferenceServer; model: string }[] {
  const seen = new Map<string, { server: InferenceServer; model: string }>();

  for (const { server, model } of [...overview.models, ...overview.series]) {
    seen.set(`${server}:${model}`, { server, model });
  }

  return [...seen.values()].sort((a, b) =>
    a.server === b.server ? a.model.localeCompare(b.model) : a.server === "Chat" ? -1 : 1,
  );
}
