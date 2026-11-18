import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, CheckCircle2, ChevronDown, ChevronRight, Gauge, Hash, Square, Timer, Trash2, Zap } from "lucide-react";
import styles from "../evaluations.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import { StatusMessage } from "@/shared/ui/status-message";
import { EvaluationStatusBadge } from "../components/evaluation-status-badge";
import { useEvaluation } from "../hooks/useEvaluations";
import { isActive, type EvaluationCase, type EvaluationRepetition } from "../types/evaluations";
import { checkInfo } from "../utils/case-draft";
import { caseMetrics, formatMs, formatNumber } from "../utils/evaluation-metrics";

export function EvaluationView() {
  const { evaluationId } = useParams();
  const navigate = useNavigate();
  const { evaluation, isLoading, error, cancel, isCancelling, remove, isDeleting } = useEvaluation(evaluationId);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const back = (
    <Button iconOnly variant="ghost" aria-label="Back to Evaluations" icon={<ArrowLeft />} onClick={() => navigate("/evaluations")} />
  );

  if (isLoading) {
    return (
      <Page title="Evaluation" leading={back}>
        <Skeleton height={240} />
      </Page>
    );
  }

  if (error || !evaluation) {
    return (
      <Page title="Evaluation" leading={back}>
        <EmptyState icon={<Gauge />} title="Evaluation not found" hint="It may have been deleted." />
      </Page>
    );
  }

  const { summary, cases } = evaluation;
  const active = isActive(summary.status);
  const all = caseMetrics(cases.flatMap((c) => c.repetitions));

  const run = async (action: () => Promise<unknown>, failure: string) => {
    setActionError(null);
    try {
      await action();
    } catch (err) {
      setActionError(err instanceof Error ? err.message : failure);
    }
  };

  return (
    <Page
      title={`${summary.suiteName} v${summary.suiteVersion}`}
      subtitle={[
        `${summary.agentName} on ${summary.model}`,
        `${summary.repetitions} ${summary.repetitions === 1 ? "repetition" : "repetitions"}`,
        ...(summary.judgeModel ? [`Judge: ${summary.judgeModel}`] : []),
      ].join(" · ")}
      width="wide"
      leading={back}
      actions={
        <div className={styles.actions}>
          <EvaluationStatusBadge status={summary.status} />
          {evaluation.modelDeleted && <Badge tone="warning">model deleted</Badge>}
          {summary.benchmarkId && (
            <Button variant="ghost" size="sm" onClick={() => navigate(`/evaluations/benchmarks/${summary.benchmarkId}`)}>
              Open Benchmark
            </Button>
          )}
          {active ? (
            <Button
              variant="secondary"
              size="sm"
              icon={<Square size={14} />}
              loading={isCancelling}
              onClick={() => void run(cancel, "Failed to cancel the Evaluation.")}
            >
              Cancel
            </Button>
          ) : (
            <Button variant="ghost" size="sm" icon={<Trash2 size={14} />} onClick={() => setConfirmDelete(true)}>
              Delete
            </Button>
          )}
        </div>
      }
    >
      {summary.error && <StatusMessage variant="error" message={summary.error} />}
      {summary.judgeModel !== null && summary.judgeModel === summary.model && (
        <StatusMessage
          variant="warning"
          message="The Judge is the model under test, so it scores its own answers and tends to rate them too kindly. Choose a different Judge model in Settings → Evaluations."
        />
      )}
      {actionError && <StatusMessage variant="error" message={actionError} />}
      {active && (
        <StatusMessage
          variant="loading"
          message={
            summary.status === "Queued"
              ? "Waiting for earlier Evaluations to finish…"
              : `Evaluating… ${summary.completedCaseCount} of ${summary.caseCount} cases done.`
          }
        />
      )}

      <StatGrid>
        <StatCard icon={<CheckCircle2 />} label="Cases passed" value={`${summary.passedCaseCount}/${summary.caseCount}`} />
        <StatCard icon={<Timer />} label="Avg latency" value={formatMs(all.latencyMs)} />
        <StatCard icon={<Hash />} label="Avg tokens in / out" value={`${formatNumber(all.inputTokens)} / ${formatNumber(all.outputTokens)}`} />
        <StatCard icon={<Zap />} label="Avg tokens/sec" value={formatNumber(all.tokensPerSecond)} />
      </StatGrid>

      <Card padding="none" as="section">
        <CardHeader icon={<Gauge />} title="Cases" description="A case passes only when every Check passes in every repetition." />
        <ul className={styles.suiteList}>
          {cases.map((c) => (
            <CaseRow key={c.caseId} evalCase={c} repetitions={summary.repetitions} />
          ))}
        </ul>
      </Card>

      <ConfirmDialog
        open={confirmDelete}
        variant="danger"
        title="Delete Evaluation"
        description="Delete this Evaluation and all its results? This cannot be undone."
        confirmLabel={isDeleting ? "Deleting…" : "Delete Evaluation"}
        cancelLabel="Cancel"
        onConfirm={() => {
          setConfirmDelete(false);
          void run(async () => {
            await remove();
            navigate("/evaluations");
          }, "Failed to delete the Evaluation.");
        }}
        onCancel={() => setConfirmDelete(false)}
      />
    </Page>
  );
}

function CaseRow({ evalCase, repetitions }: { evalCase: EvaluationCase; repetitions: number }) {
  const [open, setOpen] = useState(false);
  const metrics = caseMetrics(evalCase.repetitions);
  const label = evalCase.name ?? evalCase.prompt;

  return (
    <li className={styles.caseResult}>
      <button type="button" className={styles.caseResultHead} aria-expanded={open} onClick={() => setOpen(!open)}>
        {open ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
        <span className={styles.caseResultLabel}>{label}</span>
        {evalCase.passed === null ? (
          <Badge>pending</Badge>
        ) : (
          <Badge tone={evalCase.passed ? "success" : "danger"}>{evalCase.passed ? "passed" : "failed"}</Badge>
        )}
        {evalCase.passed !== null && (
          <span className={styles.muted}>
            {evalCase.passedRepetitions}/{repetitions} · {formatMs(metrics.latencyMs)} · {formatNumber(metrics.tokensPerSecond)} tok/s
          </span>
        )}
      </button>
      {open && (
        <div className={styles.caseResultBody}>
          <p className={styles.muted}>{evalCase.prompt}</p>
          {evalCase.repetitions.map((r) => (
            <RepetitionDetail key={r.number} repetition={r} />
          ))}
        </div>
      )}
    </li>
  );
}

function RepetitionDetail({ repetition }: { repetition: EvaluationRepetition }) {
  return (
    <section className={styles.repetition} aria-label={`Repetition ${repetition.number}`}>
      <p className={styles.groupTitle}>
        Repetition {repetition.number}{" "}
        <Badge tone={repetition.passed ? "success" : "danger"}>{repetition.passed ? "passed" : "failed"}</Badge>
      </p>
      {repetition.error && <StatusMessage variant="error" message={repetition.error} />}

      <ul className={styles.checkResults}>
        {repetition.checks.map((c) => (
          <li key={c.type}>
            <Badge tone={c.status === "Passed" ? "success" : c.status === "Failed" ? "danger" : "neutral"}>{c.status}</Badge>{" "}
            {checkInfo(c.type).label}
            {c.threshold !== null && (
              <span className={styles.muted}>
                {" "}
                · score {c.score ?? "–"}/5 (needs {c.threshold})
              </span>
            )}
            {c.reason && <span className={styles.muted}> · {c.reason}</span>}
          </li>
        ))}
      </ul>

      <div className={styles.transcript}>
        {repetition.transcript.map((m, i) => (
          <p key={i} className={m.role === "user" ? styles.userMessage : styles.assistantMessage}>
            <strong>{m.role === "user" ? "User" : "Agent"}:</strong> {m.text}
          </p>
        ))}
      </div>

      {repetition.toolCalls.length > 0 && (
        <ul className={styles.checkResults} aria-label="Tool calls">
          {repetition.toolCalls.map((call, i) => (
            <li key={i}>
              <code>
                {call.toolName}({JSON.stringify(call.arguments)})
              </code>{" "}
              <span className={styles.muted}>→ {call.result}</span>
            </li>
          ))}
        </ul>
      )}

      <p className={styles.muted}>
        {formatMs(repetition.latencyMs)} · {formatNumber(repetition.inputTokens)} in / {formatNumber(repetition.outputTokens)} out ·{" "}
        {formatNumber(repetition.tokensPerSecond)} tok/s
      </p>
    </section>
  );
}
