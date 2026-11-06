import { GripVertical, X } from "lucide-react";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import styles from "../workflows.module.css";
import type { SortableAgentItem } from "@/features/workflows/types/workflows";

interface SortableAgentProps {
  item: SortableAgentItem;
  onRemove: (sortId: string) => void;
}

export function SortableAgent({ item, onRemove }: SortableAgentProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.sortId });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`${styles.node} ${isDragging ? styles.nodeDragging : ""}`}
    >
      <button
        type="button"
        className={styles.dragHandle}
        aria-label={`Reorder ${item.name}`}
        {...attributes}
        {...listeners}
      >
        <GripVertical size={16} />
      </button>
      <div className={styles.nodeInfo}>
        <div className={styles.nodeTitleRow}>
          <span className={styles.nodeName}>{item.name}</span>
          {item.model && <span className={styles.nodeModel}>{item.model}</span>}
        </div>
        {item.description && <span className={styles.nodeDesc}>{item.description}</span>}
      </div>
      <button
        type="button"
        className={styles.nodeRemove}
        onClick={() => onRemove(item.sortId)}
        aria-label={`Remove ${item.name}`}
      >
        <X size={14} />
      </button>
    </div>
  );
}
