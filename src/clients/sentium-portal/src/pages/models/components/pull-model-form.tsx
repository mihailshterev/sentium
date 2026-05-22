import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Download, X, Loader, CheckCircle, AlertCircle } from "lucide-react";
import styles from "../models.module.scss";
import type { PullState } from "../../../hooks/useOllamaModels";
import { modelPullSchema, type ModelPullFormData } from "../../../schemas/model.pull";

function formatBytes(bytes: number): string {
  if (bytes === 0) {
    return "0 B";
  }
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

interface PullModelFormProps {
  pullState: PullState | null;
  isPulling: boolean;
  onSubmit: (modelName: string) => void;
  onCancelPull: () => void;
  onResetPull: () => void;
  getPullPercent: () => number | null;
}

const PullModelForm = ({
  pullState,
  isPulling,
  onSubmit,
  onCancelPull,
  onResetPull,
  getPullPercent,
}: PullModelFormProps) => {
  const { register, handleSubmit } = useForm<ModelPullFormData>({
    resolver: zodResolver(modelPullSchema),
    defaultValues: { modelName: "" },
  });

  const handleFormSubmit = (data: ModelPullFormData) => {
    onSubmit(data.modelName.trim());
  };

  return (
    <form className={styles.pullForm} onSubmit={handleSubmit(handleFormSubmit)}>
      <div className={styles.fieldGroup}>
        <label className={styles.fieldLabel} htmlFor="model-name-input">
          Model name
        </label>
        <input
          id="model-name-input"
          className={styles.fieldInput}
          placeholder="e.g. llama3.2, gemma3:12b"
          disabled={isPulling}
          autoComplete="off"
          spellCheck={false}
          {...register("modelName")}
        />
        <span className={styles.fieldHint}>Enter any model name available on Ollama Hub</span>
      </div>

      {!isPulling && (
        <button type="submit" className={styles.pullBtn} disabled={isPulling}>
          <Download size={14} />
          Pull Model
        </button>
      )}

      {isPulling && (
        <button type="button" className={styles.cancelBtn} onClick={onCancelPull}>
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
        <button type="button" className={styles.cancelBtn} onClick={onResetPull}>
          <X size={12} />
          Dismiss
        </button>
      )}
    </form>
  );
};

export default PullModelForm;
