import { useId, useState } from "react";
import styles from "./inference.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import type { InferenceModelState, InferencePoint, InferenceServer } from "@/features/vigil/types/inference";
import { formatCount, formatPercent, formatRate, LOCALE } from "@/features/vigil/utils/inference-format";
import { TimeSeriesChart, type TooltipRow } from "./time-series-chart";

interface ModelSeriesProps {
  server: InferenceServer;
  model: string;
  state?: InferenceModelState;
  points: InferencePoint[];
  from: number;
  to: number;
  stepMs: number;
}

const PERCENT_MAX = 100;

/**
 * One model's small multiple: its speed and how busy it was, as two single-series charts (two measures with
 * different scales never share an axis), plus a table view holding every value.
 */
export function ModelSeries({ server, model, state, points, from, to, stepMs }: ModelSeriesProps) {
  const [showTable, setShowTable] = useState(false);
  const headingId = useId();

  // The embeddings server only reads input, so its useful speed is prompt processing, not generation.
  const isEmbedding = server === "Embedding";
  const speedOf = (p: InferencePoint) => (isEmbedding ? p.promptTokensPerSecond : p.generationTokensPerSecond);
  const speedTitle = isEmbedding ? "Embedding speed · tokens/s" : "Generation speed · tokens/s";

  const speed = points.map((p) => ({ at: Date.parse(p.at), value: speedOf(p) }));
  const busy = points.map((p) => ({ at: Date.parse(p.at), value: p.busyPercent }));

  const describe = (index: number): TooltipRow[] => {
    const p = points[index];
    return [
      { label: "prompt tokens/s", value: p.promptTokensPerSecond === null ? "—" : formatRate(p.promptTokensPerSecond) },
      { label: "tokens generated", value: formatCount(p.generatedTokens) },
      { label: "busy", value: formatPercent(p.busyPercent) },
      { label: "peak waiting", value: formatCount(p.peakRequestsDeferred) },
    ];
  };

  return (
    <section className={styles.model} aria-labelledby={headingId} data-testid="inference-model">
      <header className={styles.modelHeader}>
        <h3 id={headingId} className={styles.modelName}>
          {model}
        </h3>
        <span className={styles.serverLabel}>{isEmbedding ? "embeddings" : "chat"}</span>
        {state && <LiveState state={state} />}
        <button
          type="button"
          className={styles.viewToggle}
          aria-pressed={showTable}
          onClick={() => setShowTable((v) => !v)}
          disabled={points.length === 0}
        >
          {showTable ? "Chart view" : "Table view"}
        </button>
      </header>

      {points.length === 0 ? (
        <p className={styles.empty}>No samples in this range yet.</p>
      ) : showTable ? (
        <PointsTable points={points} speedOf={speedOf} caption={`${model} by bucket`} />
      ) : (
        <div className={styles.charts}>
          <TimeSeriesChart
            title={speedTitle}
            points={speed}
            from={from}
            to={to}
            stepMs={stepMs}
            formatValue={formatRate}
            describe={describe}
          />
          <TimeSeriesChart
            title="Busy · % of samples"
            points={busy}
            from={from}
            to={to}
            stepMs={stepMs}
            max={PERCENT_MAX}
            formatValue={formatPercent}
            describe={describe}
          />
        </div>
      )}
    </section>
  );
}

function LiveState({ state }: { state: InferenceModelState }) {
  const tone: BadgeTone = state.loadFailed
    ? "danger"
    : state.status === "loaded"
      ? "success"
      : state.status === "loading"
        ? "info"
        : "neutral";

  return (
    <span className={styles.liveState}>
      <Badge tone={tone} dot>
        {state.loadFailed ? "load failed" : state.status}
      </Badge>
      {state.status === "loaded" && (
        <span className={styles.queue} data-waiting={state.requestsDeferred > 0 || undefined}>
          {state.requestsProcessing} processing · {state.requestsDeferred} waiting
        </span>
      )}
    </span>
  );
}

function PointsTable({
  points,
  speedOf,
  caption,
}: {
  points: InferencePoint[];
  speedOf: (p: InferencePoint) => number | null;
  caption: string;
}) {
  const captionId = useId();
  const time = new Intl.DateTimeFormat(LOCALE, { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

  return (
    <div className={`data-table-scroll ${styles.tableScroll}`} tabIndex={0} role="region" aria-labelledby={captionId}>
      <table className="data-table">
        <caption id={captionId} className="visually-hidden">
          {caption}
        </caption>
        <thead>
          <tr>
            <th scope="col">Time</th>
            <th scope="col">Tokens/s</th>
            <th scope="col">Prompt tokens/s</th>
            <th scope="col">Generated</th>
            <th scope="col">Busy</th>
            <th scope="col">Peak waiting</th>
          </tr>
        </thead>
        <tbody>
          {[...points].reverse().map((p) => {
            const speed = speedOf(p);
            return (
              <tr key={p.at}>
                <th scope="row" className={styles.rowHeader}>
                  {time.format(Date.parse(p.at))}
                </th>
                <td className={styles.num}>{speed === null ? "—" : formatRate(speed)}</td>
                <td className={styles.num}>{p.promptTokensPerSecond === null ? "—" : formatRate(p.promptTokensPerSecond)}</td>
                <td className={styles.num}>{formatCount(p.generatedTokens)}</td>
                <td className={styles.num}>{formatPercent(p.busyPercent)}</td>
                <td className={styles.num}>{formatCount(p.peakRequestsDeferred)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
