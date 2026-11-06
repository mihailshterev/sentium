import { ChevronDown, FileText, Pencil, Trash2 } from "lucide-react";
import styles from "../skills.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";

interface SkillLike {
  name: string;
  description: string;
  instructions: string;
}

interface SkillCardProps {
  skill: SkillLike;
  expanded: boolean;
  onToggle: () => void;
  /** Omit for read-only skills. */
  onEdit?: () => void;
  onDelete?: () => void;
  isDeleting?: boolean;
  isUpdating?: boolean;
  pill: string;
  pillTone?: BadgeTone;
  fileName?: string;
}

export function SkillCard({
  skill,
  expanded,
  onToggle,
  onEdit,
  onDelete,
  isDeleting = false,
  pill,
  pillTone = "neutral",
  fileName,
}: SkillCardProps) {
  return (
    <li className={styles.skillRow}>
      <div className={styles.skillHead}>
        <div className={styles.skillMain}>
          <div className={styles.skillTitleRow}>
            <span className={styles.skillName}>{skill.name}</span>
            <Badge tone={pillTone}>{pill}</Badge>
            {fileName && (
              <span className={styles.fileChip}>
                <FileText size={12} aria-hidden="true" />
                {fileName}
              </span>
            )}
          </div>
          <p className={styles.skillDesc}>{skill.description}</p>
        </div>

        <div className={styles.rowActions}>
          {onEdit && (
            <Button variant="ghost" size="sm" iconOnly icon={<Pencil size={15} />} onClick={onEdit} title="Edit" />
          )}
          {onDelete && (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<Trash2 size={15} />}
              onClick={onDelete}
              loading={isDeleting}
              title="Delete"
              className={styles.deleteButton}
            />
          )}
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<ChevronDown size={16} className={expanded ? styles.chevronOpen : styles.chevron} />}
            onClick={onToggle}
            title="Toggle instructions"
            aria-expanded={expanded}
          />
        </div>
      </div>
      {expanded && <pre className={styles.instructions}>{skill.instructions}</pre>}
    </li>
  );
}
