import { BrainCircuit, Power, PowerOff } from "lucide-react";
import styles from "../models.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import type { InferenceModel } from "@/features/models/types/models";

const STATUS_TONE: Record<string, BadgeTone> = {
  loaded: "success",
  loading: "warning",
  unloaded: "neutral",
  sleeping: "info",
};

function formatContext(tokens: number): string {
  return tokens >= 1024 ? `${Math.round(tokens / 1024)}k ctx` : `${tokens} ctx`;
}

interface ModelCardProps {
  model: InferenceModel;
  isDefault: boolean;
  isPending: boolean;
  canManage: boolean;
  onLoad: (id: string) => void;
  onUnload: (id: string) => void;
}

export function ModelCard({ model, isDefault, isPending, canManage, onLoad, onUnload }: ModelCardProps) {
  const isLoaded = model.status === "loaded";
  const isChanging = model.status === "loading";

  return (
    <li className={styles.modelRow}>
      <span className={styles.modelIcon} aria-hidden="true">
        <BrainCircuit size={18} />
      </span>

      <div className={styles.modelMain}>
        <p className={styles.modelName}>{model.id}</p>
        <div className={styles.specs}>
          <Badge tone={STATUS_TONE[model.status] ?? "neutral"} dot>
            {model.status}
          </Badge>
          {isDefault && <Badge tone="accent">default</Badge>}
          {model.contextLength != null && <span className={styles.spec}>{formatContext(model.contextLength)}</span>}
        </div>
      </div>

      {canManage &&
        (isLoaded ? (
          <Button
            variant="ghost"
            size="sm"
            icon={<PowerOff size={15} />}
            loading={isPending}
            onClick={() => onUnload(model.id)}
            title={`Unload ${model.id} from memory`}
          >
            Unload
          </Button>
        ) : (
          <Button
            variant="ghost"
            size="sm"
            icon={<Power size={15} />}
            loading={isPending || isChanging}
            disabled={isChanging}
            onClick={() => onLoad(model.id)}
            title={`Load ${model.id} into memory`}
          >
            Load
          </Button>
        ))}
    </li>
  );
}
