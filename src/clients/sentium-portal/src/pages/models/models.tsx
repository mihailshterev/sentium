import { BrainCircuit, RefreshCw, HardDrive, Info, X, Loader } from "lucide-react";
import styles from "./models.module.scss";
import useOllamaModels from "../../hooks/useOllamaModels";
import PageHeader from "../../components/ui/page-header";
import EmptyState from "../../components/ui/empty-state";
import ModelCard from "./components/model-card";
import PullModelForm from "./components/pull-model-form";

const Models = () => {
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

  const isPulling = pullState !== null && !pullState.done;

  const handlePull = (modelName: string) => {
    pull(modelName);
  };

  const handleDelete = (name: string) => {
    if (!confirm(`Delete model "${name}"? This cannot be undone.`)) {
      return;
    }
    deleteModel(name);
  };

  const getPullPercent = (): number | null => {
    if (!pullState?.total || !pullState.completed) {
      return null;
    }
    return Math.round((pullState.completed / pullState.total) * 100);
  };

  return (
    <div className={styles.pageContainer}>
      <PageHeader
        icon={<BrainCircuit size={20} className={styles.headerIcon} />}
        title="Model Management"
        subtitle="Manage local Ollama models"
        right={
          <div className={styles.headerRight}>
            <div className={styles.headerBadge}>
              <HardDrive size={11} />
              {models.length} installed
            </div>
            <button
              className={styles.refreshBtn}
              onClick={() => refetch()}
              disabled={isLoading}
              title="Refresh model list"
            >
              <RefreshCw size={14} className={isLoading ? styles.spinIcon : undefined} />
            </button>
          </div>
        }
      />

      <div className={styles.pageBody}>
        <div className={styles.listPanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot} />
            Installed Models
            <span className={styles.countBadge}>{models.length}</span>
          </div>

          <div className={styles.modelList}>
            {deleteResult && (
              <div className={styles.deleteNotice}>
                <Info size={14} />
                <span>
                  <strong>{deleteResult.deletedModel}</strong> deleted.
                  {deleteResult.agentsReset > 0
                    ? ` ${deleteResult.agentsReset} agent${deleteResult.agentsReset !== 1 ? "s" : ""} reset to ${deleteResult.defaultModel}.`
                    : " No agents were affected."}
                </span>
                <button className={styles.noticeDismiss} onClick={clearDeleteResult} aria-label="Dismiss">
                  <X size={12} />
                </button>
              </div>
            )}
            {isLoading ? (
              <EmptyState icon={<Loader size={28} className={styles.spinIcon} />} title="Loading models…" />
            ) : models.length === 0 ? (
              <EmptyState
                icon={<BrainCircuit size={36} />}
                title="No models installed"
                hint="Pull a model using the form on the right to get started."
              />
            ) : (
              models.map((model) => (
                <ModelCard key={model.name} model={model} deletingModel={deletingModel} onDelete={handleDelete} />
              ))
            )}
          </div>
        </div>

        <div className={styles.sidePanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot} />
            Download Model
          </div>
          <PullModelForm
            pullState={pullState}
            isPulling={isPulling}
            onSubmit={handlePull}
            onCancelPull={cancelPull}
            onResetPull={resetPull}
            getPullPercent={getPullPercent}
          />
        </div>
      </div>
    </div>
  );
};

export default Models;
