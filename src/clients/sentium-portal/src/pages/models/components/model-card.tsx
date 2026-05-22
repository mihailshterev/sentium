import { Cpu, HardDrive, Hash, Trash2, Loader } from "lucide-react";
import styles from "../models.module.scss";
import type { OllamaModel } from "../../../services/agentRuntime.service";

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

interface ModelCardProps {
  model: OllamaModel;
  deletingModel: string | null;
  onDelete: (name: string) => void;
}

const ModelCard = ({ model, deletingModel, onDelete }: ModelCardProps) => (
  <div className={styles.modelCard}>
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
      onClick={() => onDelete(model.name)}
      disabled={deletingModel === model.name}
      title={`Delete ${model.name}`}
    >
      {deletingModel === model.name ? <Loader size={14} className={styles.spinIcon} /> : <Trash2 size={14} />}
    </button>
  </div>
);

export default ModelCard;
