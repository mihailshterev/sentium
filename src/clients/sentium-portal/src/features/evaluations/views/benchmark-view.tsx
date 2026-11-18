import { Fragment, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ArrowLeft, ChevronDown, ChevronRight, Columns3, Square, Trash2 } from "lucide-react";
import styles from "../evaluations.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { EvaluationStatusBadge } from "../components/evaluation-status-badge";
import { useBenchmark } from "../hooks/useBenchmarks";
import { isActive } from "../types/evaluations";
import { buildComparison, type CaseCell, type CheckCell, type ModelColumn } from "../utils/benchmark-comparison";
import { checkInfo } from "../utils/case-draft";
import { formatMs, formatNumber } from "../utils/evaluation-metrics";

const percent = (rate: number | null) => (rate === null ? "–" : `${Math.round(rate * 100)}%`);

export function BenchmarkView() {
  const { benchmarkId } = useParams();
  const navigate = useNavigate();
  const { benchmark, isLoading, error, cancel, isCancelling, remove } = useBenchmark(benchmarkId);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const back = (
    <Button iconOnly variant="ghost" aria-label="Back to Evaluations" icon={<ArrowLeft />} onClick={() => navigate("/evaluations")} />
  );

  if (isLoading) {
    return (
      <Page title="Benchmark" leading={back}>
        <Skeleton height={240} />
      </Page>
    );
  }

  if (error || !benchmark) {
    return (
      <Page title="Benchmark" leading={back}>
        <EmptyState icon={<Columns3 />} title="Benchmark not found" hint="It may have been deleted." />
      </Page>
    );
  }

  const comparison = buildComparison(benchmark.evaluations);
  const activeCount = benchmark.evaluations.filter((e) => isActive(e.summary.status)).length;
  const judgeIsBenchmarked = benchmark.judgeModel !== null && comparison.columns.some((c) => c.model === benchmark.judgeModel);

  const run = async (action: () => Promise<unknown>, failure: string) => {
    setActionError(null);
    try {
      await action();
    } catch (err) {
      setActionError(err instanceof Error ? err.message : failure);
    }
  };

  const toggle = (caseId: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (!next.delete(caseId)) {
        next.add(caseId);
      }
      return next;
    });

  return (
    <Page
      title={`${benchmark.suiteName} v${benchmark.suiteVersion}`}
      subtitle={[
        `Benchmark of ${benchmark.agentName}`,
        `${comparison.columns.length} models`,
        `${benchmark.repetitions} ${benchmark.repetitions === 1 ? "repetition" : "repetitions"}`,
        ...(benchmark.judgeModel ? [`Judge: ${benchmark.judgeModel}`] : []),
      ].join(" · ")}
      width="full"
      leading={back}
      actions={
        activeCount > 0 ? (
          <Button
            variant="secondary"
            size="sm"
            icon={<Square size={14} />}
            loading={isCancelling}
            onClick={() => void run(cancel, "Failed to cancel the Benchmark.")}
          >
            Cancel
          </Button>
        ) : (
          <Button variant="ghost" size="sm" icon={<Trash2 size={14} />} onClick={() => setConfirmDelete(true)}>
            Delete
          </Button>
        )
      }
    >
      {actionError && <StatusMessage variant="error" message={actionError} />}
      {activeCount > 0 && (
        <StatusMessage
          variant="loading"
          message={`Evaluating one model at a time… ${comparison.columns.length - activeCount} of ${comparison.columns.length} done.`}
        />
      )}
      {judgeIsBenchmarked && (
        <StatusMessage
          variant="warning"
          message={`The Judge (${benchmark.judgeModel}) is also one of the benchmarked models, so it scores its own answers and tends to rate them too kindly.`}
        />
      )}

      <Card padding="none" as="section">
        <CardHeader icon={<Columns3 />} title="Overview" description="Averages over every graded repetition. The best value of each row is highlighted." />
        <div className="data-table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Measure</th>
                {comparison.columns.map((c) => (
                  <th scope="col" key={c.evaluationId}>
                    <ModelHeading column={c} />
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              <MetricRow
                label="Cases passed"
                columns={comparison.columns}
                best={comparison.best.passRate}
                value={(c) => `${c.passedCases}/${c.caseCount} · ${percent(c.passRate)}`}
              />
              <MetricRow label="Avg latency" columns={comparison.columns} best={comparison.best.latencyMs} value={(c) => formatMs(c.latencyMs)} />
              <MetricRow
                label="Avg tokens/sec"
                columns={comparison.columns}
                best={comparison.best.tokensPerSecond}
                value={(c) => formatNumber(c.tokensPerSecond)}
              />
              <MetricRow
                label="Avg tokens in / out"
                columns={comparison.columns}
                best={null}
                value={(c) => `${formatNumber(c.inputTokens)} / ${formatNumber(c.outputTokens)}`}
              />
            </tbody>
          </table>
        </div>
      </Card>

      <Card padding="none" as="section">
        <CardHeader icon={<Columns3 />} title="Cases" description="Open a case to compare its Checks. Counts are passing repetitions." />
        <div className="data-table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Case</th>
                {comparison.columns.map((c) => (
                  <th scope="col" key={c.evaluationId}>
                    {c.model}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {comparison.cases.map((row) => {
                const open = expanded.has(row.caseId);
                return (
                  <Fragment key={row.caseId}>
                    <tr>
                      <td>
                        <button type="button" className={styles.tableToggle} aria-expanded={open} onClick={() => toggle(row.caseId)}>
                          {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                          <span className={styles.caseResultLabel}>{row.name ?? row.prompt}</span>
                        </button>
                      </td>
                      {row.cells.map((cell, i) => (
                        <td key={comparison.columns[i].evaluationId}>
                          <CaseResult cell={cell} />
                        </td>
                      ))}
                    </tr>
                    {open &&
                      row.checks.map((type) => (
                        <tr key={`${row.caseId}-${type}`} className={styles.checkRow}>
                          <td>{checkInfo(type).label}</td>
                          {row.cells.map((cell, i) => (
                            <td key={comparison.columns[i].evaluationId}>
                              <CheckResult check={cell.checks[type]} />
                            </td>
                          ))}
                        </tr>
                      ))}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </Card>

      <ConfirmDialog
        open={confirmDelete}
        variant="danger"
        title="Delete Benchmark"
        description="Delete this Benchmark and all its Evaluations? This cannot be undone."
        confirmLabel="Delete Benchmark"
        cancelLabel="Cancel"
        onConfirm={() => {
          setConfirmDelete(false);
          void run(async () => {
            await remove();
            navigate("/evaluations");
          }, "Failed to delete the Benchmark.");
        }}
        onCancel={() => setConfirmDelete(false)}
      />
    </Page>
  );
}

function ModelHeading({ column }: { column: ModelColumn }) {
  return (
    <span className={styles.modelHeading}>
      <Link to={`/evaluations/${column.evaluationId}`}>{column.model}</Link>
      <EvaluationStatusBadge status={column.status} />
      {column.modelDeleted && <Badge tone="warning">deleted</Badge>}
    </span>
  );
}

function MetricRow({
  label,
  columns,
  best,
  value,
}: {
  label: string;
  columns: ModelColumn[];
  best: number | null;
  value: (column: ModelColumn) => string;
}) {
  return (
    <tr>
      <td className={styles.rowLabel}>{label}</td>
      {columns.map((c, i) => (
        <td key={c.evaluationId} className={i === best ? styles.best : undefined}>
          {value(c)}
        </td>
      ))}
    </tr>
  );
}

function CaseResult({ cell }: { cell: CaseCell }) {
  if (cell.passed === null) {
    return <Badge>pending</Badge>;
  }
  return (
    <Badge tone={cell.passed ? "success" : "danger"}>
      {cell.passed ? "passed" : "failed"} {cell.passedRepetitions}/{cell.repetitions}
    </Badge>
  );
}

function CheckResult({ check }: { check: CheckCell | undefined }) {
  if (!check || check.graded === 0) {
    return <span className={styles.muted}>–</span>;
  }
  return (
    <span className={check.passed === check.graded ? undefined : styles.failedText}>
      {check.passed}/{check.graded}
      {check.averageScore !== null && <span className={styles.muted}> · score {check.averageScore.toFixed(1)}</span>}
    </span>
  );
}
