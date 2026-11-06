import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Download, X } from "lucide-react";
import styles from "../models.module.css";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import type { PullState } from "@/features/models/hooks/useOllamaModels";
import { modelPullSchema, type ModelPullFormData } from "@/features/models/schemas/model.pull";

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

export function PullModelForm({
  pullState,
  isPulling,
  onSubmit,
  onCancelPull,
  onResetPull,
  getPullPercent,
}: PullModelFormProps) {
  const { register, handleSubmit, watch } = useForm<ModelPullFormData>({
    resolver: zodResolver(modelPullSchema),
    defaultValues: { modelName: "" },
  });

  // eslint-disable-next-line react-hooks/incompatible-library
  const modelName = watch("modelName") ?? "";
  const percent = getPullPercent();

  return (
    <form className={styles.pullForm} onSubmit={handleSubmit((data) => onSubmit(data.modelName.trim()))}>
      <FormField id="model-name-input" label="Model name" hint="Any model name available on the Ollama library">
        <Input
          id="model-name-input"
          placeholder="e.g. llama3.2, gemma3:12b"
          disabled={isPulling}
          autoComplete="off"
          spellCheck={false}
          {...register("modelName")}
        />
      </FormField>

      {isPulling ? (
        <Button variant="secondary" fullWidth icon={<X size={15} />} onClick={onCancelPull}>
          Cancel
        </Button>
      ) : (
        <Button type="submit" variant="primary" fullWidth icon={<Download size={16} />} disabled={!modelName.trim()}>
          Pull Model
        </Button>
      )}

      {pullState &&
        (pullState.error ? (
          <StatusMessage variant="error" message={pullState.error} />
        ) : pullState.done ? (
          <StatusMessage variant="success" message="Model pulled successfully" />
        ) : (
          <div className={styles.progress} aria-live="polite">
            <div className={styles.progressStatus}>
              <span className={styles.progressText} title={pullState.status}>
                {pullState.status || "Connecting…"}
              </span>
              {percent !== null && <span className={styles.progressPercent}>{percent}%</span>}
            </div>
            <div className={styles.progressTrack}>
              {percent !== null ? (
                <div className={styles.progressFill} style={{ width: `${percent}%` }} />
              ) : (
                <div className={styles.progressIndeterminate} />
              )}
            </div>
            {percent !== null && (
              <span className={styles.progressBytes}>
                {formatBytes(pullState.completed ?? 0)} / {formatBytes(pullState.total ?? 0)}
              </span>
            )}
          </div>
        ))}

      {pullState?.done && (
        <Button variant="ghost" size="sm" icon={<X size={14} />} onClick={onResetPull} className={styles.dismiss}>
          Dismiss
        </Button>
      )}
    </form>
  );
}
