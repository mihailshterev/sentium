import { ChevronDown, ChevronUp, Loader, Pencil, Trash2 } from "lucide-react";
import styles from "../skills.module.scss";
import type { AgentSkill } from "../../../types/skills";

interface SkillCardProps {
  skill: AgentSkill;
  expanded: boolean;
  onToggle: () => void;
  onEdit: () => void;
  onDelete: () => void;
  isDeleting: boolean;
  isUpdating: boolean;
  pill: string;
  pillClass: string;
  fileName?: string;
}

const SkillCard = ({
  skill,
  expanded,
  onToggle,
  onEdit,
  onDelete,
  isDeleting,
  pill,
  pillClass,
  fileName,
}: SkillCardProps) => (
  <div className={styles.skillRow}>
    <div className={styles.skillRowHeader}>
      <div className={styles.skillMeta}>
        <span className={styles.skillName}>{skill.name}</span>
        <span className={pillClass}>{pill}</span>
        {fileName && <span className={styles.fileChip}>{fileName}</span>}
      </div>
      <div className={styles.rowActions}>
        <button className={styles.btnIcon} onClick={onEdit} title="Edit">
          <Pencil size={13} />
        </button>
        <button
          className={`${styles.btnIcon} ${styles.btnDanger}`}
          onClick={onDelete}
          disabled={isDeleting}
          title="Delete"
        >
          {isDeleting ? <Loader size={13} className={styles.spin} /> : <Trash2 size={13} />}
        </button>
        <button className={styles.btnIcon} onClick={onToggle} title="Toggle instructions">
          {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        </button>
      </div>
    </div>
    <p className={styles.skillDesc}>{skill.description}</p>
    {expanded && <pre className={styles.instructions}>{skill.instructions}</pre>}
  </div>
);

export default SkillCard;
