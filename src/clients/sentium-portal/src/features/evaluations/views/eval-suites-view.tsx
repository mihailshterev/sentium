import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { FlaskConical, Plus, Trash2 } from "lucide-react";
import styles from "../evaluations.module.css";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatDateTimeShort } from "@/shared/utils/formatters";
import { BenchmarkList } from "../components/benchmark-list";
import { EvaluationList } from "../components/evaluation-list";
import { useEvalSuites } from "../hooks/useEvalSuites";
import type { EvalSuiteSummary } from "../types/eval-suites";

export function EvalSuitesView() {
  const navigate = useNavigate();
  const { suites, isLoading, error, hasMore, loadMore, isLoadingMore, deleteSuite, isDeleting } = useEvalSuites();
  const [toDelete, setToDelete] = useState<EvalSuiteSummary | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const confirmDelete = async () => {
    if (!toDelete) {
      return;
    }
    setDeleteError(null);
    try {
      await deleteSuite(toDelete.id);
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : "Failed to delete the suite.");
    } finally {
      setToDelete(null);
    }
  };

  return (
    <Page title="Evaluations" subtitle="Measure how well an agent does on a model with your own Eval Suites">
      <Card padding="none" as="section">
        <CardHeader
          icon={<FlaskConical />}
          title="Eval Suites"
          description="Private to you. Editing a suite's cases creates a new version; earlier versions stay readable."
          actions={
            <Button variant="primary" size="sm" icon={<Plus size={15} />} onClick={() => navigate("/evaluations/suites/new")}>
              New Suite
            </Button>
          }
        />

        {error && <StatusMessage variant="error" message="Failed to load Eval Suites." />}
        {deleteError && <StatusMessage variant="error" message={deleteError} />}

        {isLoading ? (
          <div className={styles.section}>
            <SkeletonText lines={3} />
          </div>
        ) : suites.length === 0 ? (
          <EmptyState
            icon={<FlaskConical />}
            title="No Eval Suites yet"
            hint="Write prompts with the Checks their answers must pass, then evaluate agents against them."
          />
        ) : (
          <ul className={styles.suiteList}>
            {suites.map((suite) => (
              <li className={styles.suiteRow} key={suite.id}>
                <Link className={styles.suiteMain} to={`/evaluations/suites/${suite.id}`}>
                  <span className={styles.suiteTitleRow}>
                    <span className={styles.suiteName}>{suite.name}</span>
                    <Badge tone="purple">v{suite.currentVersion}</Badge>
                    <Badge>
                      {suite.caseCount} {suite.caseCount === 1 ? "case" : "cases"}
                    </Badge>
                  </span>
                  {suite.description && <span className={styles.muted}>{suite.description}</span>}
                  <span className={styles.muted}>Updated {formatDateTimeShort(suite.updatedAt)}</span>
                </Link>
                <Button
                  iconOnly
                  variant="ghost"
                  aria-label={`Delete ${suite.name}`}
                  icon={<Trash2 size={15} />}
                  loading={isDeleting && toDelete?.id === suite.id}
                  onClick={() => setToDelete(suite)}
                />
              </li>
            ))}
          </ul>
        )}
        <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={loadMore} />
      </Card>

      <BenchmarkList />

      <EvaluationList />

      <ConfirmDialog
        open={toDelete !== null}
        variant="danger"
        title="Delete Eval Suite"
        description={`Delete "${toDelete?.name ?? ""}" and all its versions? This cannot be undone.`}
        confirmLabel="Delete Suite"
        cancelLabel="Cancel"
        onConfirm={() => void confirmDelete()}
        onCancel={() => setToDelete(null)}
      />
    </Page>
  );
}
