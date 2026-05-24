import { Bot, GripVertical, X } from "lucide-react";
import { useSortable } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import styles from "../workflows.module.scss";
import type { SortableAgentItem } from "../../../types/workflows";

interface SortableAgentProps {
  item: SortableAgentItem;
  onRemove: (sortId: string) => void;
}

const SortableAgent = ({ item, onRemove }: SortableAgentProps) => {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.sortId });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div ref={setNodeRef} style={style} className={styles.sortableAgent}>
      <button type="button" className={styles.dragHandle} {...attributes} {...listeners}>
        <GripVertical size={14} />
      </button>
      <Bot size={22} className={styles.sortableAgentIcon} />
      <div className={styles.sortableAgentInfo}>
        <span className={styles.sortableAgentName}>{item.name}</span>
        <div className={styles.sortableAgentMeta}>
          <span className={styles.sortableAgentModel}>{item.model}</span>
        </div>
        {item.description && <span className={styles.sortableAgentDesc}>{item.description}</span>}
      </div>
      <button className={styles.sortableAgentRemove} onClick={() => onRemove(item.sortId)}>
        <X size={12} />
      </button>
    </div>
  );
};

export default SortableAgent;
