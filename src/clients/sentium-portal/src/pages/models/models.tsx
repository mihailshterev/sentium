import { useState } from "react";
import {
  BrainCircuit,
  Download,
  Trash2,
  RefreshCw,
  CheckCircle,
  AlertCircle,
  Loader,
  X,
  HardDrive,
  Cpu,
  Hash,
  Info,
} from "lucide-react";
import styles from "./models.module.scss";
import useOllamaModels from "../../hooks/useOllamaModels";

function formatBytes(bytes: number): string {
  if (bytes === 0) {
    return "0 B";
  }
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
}

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

  const [modelName, setModelName] = useState("");
  const isPulling = pullState !== null && !pullState.done;

  const handlePull = (e: React.FormEvent) => {
    e.preventDefault();
    const name = modelName.trim();
    if (!name) {
      return;
    }
    pull(name);
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
      <div className={styles.pageHeader}>
        <div className={styles.headerLeft}>
          <BrainCircuit size={20} className={styles.headerIcon} />
          <div>
            <h2 className={styles.headerTitle}>Model Management</h2>
            <span className={styles.headerSub}>Manage local Ollama models</span>
          </div>
        </div>
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
      </div>

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
              <div className={styles.emptyState}>
                <Loader size={28} className={`${styles.emptyIcon} ${styles.spinIcon}`} />
                <span>Loading models…</span>
              </div>
            ) : models.length === 0 ? (
              <div className={styles.emptyState}>
                <BrainCircuit size={36} className={styles.emptyIcon} />
                <span>No models installed</span>
                <span className={styles.emptyHint}>Pull a model using the form on the right to get started.</span>
              </div>
            ) : (
              models.map((model) => (
                <div key={model.name} className={styles.modelCard}>
                  <div className={styles.modelIconWrap}>
                    <Cpu size={16} />
                  </div>

                  <div className={styles.modelInfo}>
                    <p className={styles.modelName}>{model.name}</p>
                    <div className={styles.modelMeta}>
                      <span className={styles.metaBadgeGreen}>
                        <HardDrive size={10} />
                        {formatBytes(model.size)}
                      </span>
                      {model.details?.parameter_size && (
                        <span className={styles.metaBadgeBlue}>
                          <Hash size={10} />
                          {model.details.parameter_size}
                        </span>
                      )}
                      {model.details?.quantization_level && (
                        <span className={styles.metaBadge}>{model.details.quantization_level}</span>
                      )}
                      {model.details?.family && <span className={styles.metaBadge}>{model.details.family}</span>}
                    </div>
                    <p className={styles.modelDate}>Modified {formatDate(model.modified_at)}</p>
                  </div>

                  <button
                    className={styles.deleteBtn}
                    onClick={() => handleDelete(model.name)}
                    disabled={deletingModel === model.name}
                    title={`Delete ${model.name}`}
                  >
                    {deletingModel === model.name ? (
                      <Loader size={14} className={styles.spinIcon} />
                    ) : (
                      <Trash2 size={14} />
                    )}
                  </button>
                </div>
              ))
            )}
          </div>
        </div>

        <div className={styles.sidePanel}>
          <div className={styles.panelHeader}>
            <span className={styles.panelDot} />
            Download Model
          </div>

          <form className={styles.pullForm} onSubmit={handlePull}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="model-name-input">
                Model name
              </label>
              <input
                id="model-name-input"
                className={styles.fieldInput}
                placeholder="e.g. llama3.2, gemma3:12b"
                value={modelName}
                onChange={(e) => setModelName(e.target.value)}
                disabled={isPulling}
                autoComplete="off"
                spellCheck={false}
              />
              <span className={styles.fieldHint}>Enter any model name available on Ollama Hub</span>
            </div>

            {!isPulling && (
              <button type="submit" className={styles.pullBtn} disabled={!modelName.trim() || isPulling}>
                <Download size={14} />
                Pull Model
              </button>
            )}

            {isPulling && (
              <button type="button" className={styles.cancelBtn} onClick={cancelPull}>
                <X size={14} />
                Cancel
              </button>
            )}

            {pullState && (
              <div className={styles.progressBlock}>
                {pullState.error ? (
                  <div className={styles.progressError}>
                    <AlertCircle size={14} />
                    {pullState.error}
                  </div>
                ) : pullState.done ? (
                  <div className={styles.progressSuccess}>
                    <CheckCircle size={14} />
                    Model pulled successfully
                  </div>
                ) : (
                  <>
                    <div className={styles.progressStatus}>
                      <Loader size={12} className={styles.spinIcon} />
                      <span className={styles.progressStatusText} title={pullState.status}>
                        {pullState.status || "Connecting…"}
                      </span>
                    </div>

                    <div className={styles.progressBarOuter}>
                      {getPullPercent() !== null ? (
                        <div className={styles.progressBarInner} style={{ width: `${getPullPercent()}%` }} />
                      ) : (
                        <div className={styles.progressBarIndeterminate} />
                      )}
                    </div>

                    {getPullPercent() !== null && (
                      <div className={styles.progressNums}>
                        {formatBytes(pullState.completed ?? 0)} / {formatBytes(pullState.total ?? 0)}&nbsp; (
                        {getPullPercent()}%)
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {pullState?.done && (
              <button type="button" className={styles.cancelBtn} onClick={resetPull}>
                <X size={12} />
                Dismiss
              </button>
            )}
          </form>
        </div>
      </div>
    </div>
  );
};

export default Models;
