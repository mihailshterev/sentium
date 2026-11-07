import { AlertTriangle, BrainCircuit, FileCode2, Lock, RefreshCw, X } from "lucide-react";
import styles from "../models.module.css";
import { useModelCatalog } from "@/features/models/hooks/useModelCatalog";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { ModelCard } from "../components/model-card";

export function ModelsView() {
  const {
    models,
    defaultModel,
    isLoading,
    isError,
    refetch,
    load,
    unload,
    pendingModel,
    actionError,
    clearActionError,
  } = useModelCatalog();
  const { isSovereign } = useRole();

  const loadedCount = models.filter((m) => m.status === "loaded").length;

  return (
    <Page
      title="Model Management"
      subtitle="Chat models served by the local inference server"
      actions={
        <>
          <Badge tone="neutral">
            {loadedCount} / {models.length} loaded
          </Badge>
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
          <CardHeader title="Chat Models" description="Declared on the inference server; load to keep in memory" />

          {actionError && (
            <div className={styles.notice} role="alert">
              <AlertTriangle size={16} aria-hidden="true" />
              <span>{actionError.message}</span>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                icon={<X size={14} />}
                onClick={clearActionError}
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
          ) : isError ? (
            <div className={styles.errorState}>
              <StatusMessage
                variant="error"
                icon={<AlertTriangle />}
                message="The inference server is unavailable. Check the llm container."
              />
            </div>
          ) : models.length === 0 ? (
            <EmptyState
              icon={<BrainCircuit />}
              title="No models available"
              hint="The inference server reports no chat models. Declare one in infra/llm/models.ini."
            />
          ) : (
            <ul className={styles.modelList}>
              {models.map((model) => (
                <ModelCard
                  key={model.id}
                  model={model}
                  isDefault={model.id === defaultModel}
                  isPending={pendingModel === model.id}
                  canManage={isSovereign}
                  onLoad={load}
                  onUnload={unload}
                />
              ))}
            </ul>
          )}
        </Card>

        <Card padding="md" as="section" className={styles.sideCard}>
          <CardHeader icon={<FileCode2 />} title="Adding Models" description="Models are deployment configuration" />
          <div className={styles.howTo}>
            <p>
              Each chat model is a section in <code>infra/llm/models.ini</code>; its file is pinned by checksum in{" "}
              <code>infra/llm/models.lock</code>. The models-init container downloads and verifies new files when the
              stack starts.
            </p>
            <p>
              Unloaded models load on their first request. Loading one ahead of time avoids that wait; unloading frees
              GPU memory.
            </p>
          </div>
          {!isSovereign && (
            <div className={styles.lockedNote}>
              <Lock size={16} aria-hidden="true" />
              Loading and unloading models requires Sovereign access.
            </div>
          )}
        </Card>
      </div>
    </Page>
  );
}
