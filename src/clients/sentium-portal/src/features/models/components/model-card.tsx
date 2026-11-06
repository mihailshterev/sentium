import { BrainCircuit, Trash2 } from "lucide-react";
import styles from "../models.module.css";
import { Button } from "@/shared/ui/button";
import type { OllamaModel } from "@/features/models/types/models";

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
  return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
}

interface ModelCardProps {
  model: OllamaModel;
  deletingModel: string | null;
  onDelete: (name: string) => void;
  canDelete: boolean;
}

export function ModelCard({ model, deletingModel, onDelete, canDelete }: ModelCardProps) {
  const details = model.details;
  return (
    <li className={styles.modelRow}>
      <span className={styles.modelIcon} aria-hidden="true">
        <BrainCircuit size={18} />
      </span>

      <div className={styles.modelMain}>
        <p className={styles.modelName}>{model.name}</p>
        <div className={styles.specs}>
          <span className={styles.spec}>{formatBytes(model.size)}</span>
          {details?.parameter_size && <span className={styles.spec}>{details.parameter_size}</span>}
          {details?.quantization_level && <span className={styles.spec}>{details.quantization_level}</span>}
          {details?.family && <span className={styles.spec}>{details.family}</span>}
        </div>
      </div>

      <span className={styles.modelDate}>Modified {formatDate(model.modified_at)}</span>

      {canDelete && (
        <Button
          variant="ghost"
          size="sm"
          iconOnly
          icon={<Trash2 size={15} />}
          loading={deletingModel === model.name}
          onClick={() => onDelete(model.name)}
          title={`Delete ${model.name}`}
          className={styles.deleteButton}
        />
      )}
    </li>
  );
}
