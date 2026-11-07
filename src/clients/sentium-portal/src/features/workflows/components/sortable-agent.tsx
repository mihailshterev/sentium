import { Bot, X } from "lucide-react";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import styles from "../workflows.module.css";
import { GraphEdge, GraphRow } from "./graph-parts";
import type { SortableAgentItem } from "@/features/workflows/types/workflows";

interface SortableAgentProps {
  item: SortableAgentItem;
  /** Position in the pipeline (0-based); drives the step label and the edge pulse timing. */
  index?: number;
  onRemove: (sortId: string) => void;
}

/** An agent node in the workflow graph, with its incoming edge. The node disc is the drag handle. */
export function SortableAgent({ item, index = 0, onRemove }: SortableAgentProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id: item.sortId,
  });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Translate.toString(transform), transition }}
      className={isDragging ? `${styles.graphItem} ${styles.graphItemDragging}` : styles.graphItem}
    >
      <GraphEdge index={index} />
      <GraphRow
        left={
          <>
            <span className={styles.graphTag}>step {String(index + 1).padStart(2, "0")}</span>
            {item.model && <span className={styles.graphModel}>{item.model}</span>}
          </>
        }
        node={
          <span className={styles.graphNodeWrap}>
            <button
              type="button"
              ref={setActivatorNodeRef}
              className={`${styles.graphNode} ${styles.graphNode_agent}`}
              aria-label={`Reorder ${item.name}`}
              {...attributes}
              {...listeners}
            >
              <Bot size={18} aria-hidden="true" />
            </button>
            <button
              type="button"
              className={styles.graphRemove}
              onClick={() => onRemove(item.sortId)}
              aria-label={`Remove ${item.name}`}
            >
              <X size={12} aria-hidden="true" />
            </button>
          </span>
        }
        label={item.name}
        right={item.description && <span className={styles.graphDesc}>{item.description}</span>}
      />
    </li>
  );
}
