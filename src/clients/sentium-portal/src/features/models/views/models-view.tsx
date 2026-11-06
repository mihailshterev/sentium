import { useState } from "react";
import { BrainCircuit, Download, Info, Lock, RefreshCw, X } from "lucide-react";
import styles from "../models.module.css";
import { useOllamaModels } from "@/features/models/hooks/useOllamaModels";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { ModelCard } from "../components/model-card";
import { PullModelForm } from "../components/pull-model-form";

export function ModelsView() {
  const {
    models,
    isLoading,
    refetch,
    pullState,
    pull,
    cancelPull,
    resetPull,
    deletingModel,
    deleteModel,
    deleteResult,
    clearDeleteResult,
  } = useOllamaModels();
  const { isSovereign } = useRole();

  const [pendingDeleteName, setPendingDeleteName] = useState<string | null>(null);

  const isPulling = pullState !== null && !pullState.done;

  const confirmDelete = () => {
    if (!pendingDeleteName) {
      return;
    }
    deleteModel(pendingDeleteName);
    setPendingDeleteName(null);
  };

  const getPullPercent = (): number | null => {
    if (!pullState?.total || !pullState.completed) {
      return null;
    }
    return Math.round((pullState.completed / pullState.total) * 100);
  };

  return (
    <Page
      title="Model Management"
      subtitle="Manage local Ollama models"
      actions={
        <>
          <Badge tone="neutral">{models.length} installed</Badge>
          <Button
            variant="secondary"
            size="sm"
            iconOnly
            icon={<RefreshCw size={15} className={isLoading ? "animate-spin" : undefined} />}
            onClick={() => refetch()}
            disabled={isLoading}
            title="Refresh model list"
          />
        </>
      }
    >
      <div className={styles.layout}>
        <Card padding="none" as="section">
          <CardHeader title="Installed Models" description="Pulled into the local Ollama instance" />

          {deleteResult && (
            <div className={styles.notice} role="status">
              <Info size={16} aria-hidden="true" />
              <span>
                <strong>{deleteResult.deletedModel}</strong> deleted.
                {deleteResult.agentsReset > 0
                  ? ` ${deleteResult.agentsReset} agent${deleteResult.agentsReset !== 1 ? "s" : ""} reset to ${deleteResult.defaultModel}.`
                  : " No agents were affected."}
              </span>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                icon={<X size={14} />}
                onClick={clearDeleteResult}
                aria-label="Dismiss"
              />
            </div>
          )}

          {isLoading ? (
            <>
              <span className="visually-hidden">Loading models…</span>
              <ul className={styles.modelList} aria-busy="true">
                {[0, 1, 2].map((i) => (
                  <li key={i} className={styles.modelRow}>
                    <Skeleton width={36} height={36} radius="md" />
                    <span className={styles.skeletonText}>
                      <Skeleton width="35%" />
                      <Skeleton width="55%" />
                    </span>
                  </li>
                ))}
              </ul>
            </>
          ) : models.length === 0 ? (
            <EmptyState
              icon={<BrainCircuit />}
              title="No models installed"
              hint="Pull a model using the form on the right to get started."
            />
          ) : (
            <ul className={styles.modelList}>
              {models.map((model) => (
                <ModelCard
                  key={model.name}
                  model={model}
                  deletingModel={deletingModel}
                  onDelete={setPendingDeleteName}
                  canDelete={isSovereign}
                />
              ))}
            </ul>
          )}
        </Card>

        <Card padding="md" as="section" className={styles.sideCard}>
          <CardHeader icon={<Download />} title="Download Model" description="Pull from the Ollama library" />
          {isSovereign ? (
            <PullModelForm
              pullState={pullState}
              isPulling={isPulling}
              onSubmit={pull}
              onCancelPull={cancelPull}
              onResetPull={resetPull}
              getPullPercent={getPullPercent}
            />
          ) : (
            <div className={styles.lockedNote}>
              <Lock size={16} aria-hidden="true" />
              Model installation and deletion requires Sovereign access.
            </div>
          )}
        </Card>
      </div>

      <ConfirmDialog
        open={pendingDeleteName !== null}
        variant="danger"
        title="Delete model?"
        description={`"${pendingDeleteName}" will be removed from Ollama. Any agents using it will be reset to the default model.`}
        confirmLabel="Delete model"
        confirmWord={pendingDeleteName ?? undefined}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteName(null)}
      />
    </Page>
  );
}
