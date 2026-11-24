import { useState } from "react";
import { Check, Pencil, Trash2, X } from "lucide-react";
import styles from "../knowledge-base.module.css";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { Textarea } from "@/shared/ui/form-controls";
import { MAX_MEMORY_LENGTH, type UserMemory } from "../types/user-memory";

interface MemoryCardProps {
  memory: UserMemory;
  isSaving: boolean;
  isDeleting: boolean;
  onSave: (content: string) => void;
  onDelete: () => void;
  formatDate: (iso: string) => string;
}

export function MemoryCard({ memory, isSaving, isDeleting, onSave, onDelete, formatDate }: MemoryCardProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(memory.content);
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const startEditing = () => {
    setDraft(memory.content);
    setEditing(true);
  };

  const save = () => {
    if (!draft.trim()) {
      return;
    }
    onSave(draft.trim());
    setEditing(false);
  };

  const edited = memory.updatedAt !== memory.createdAt;

  return (
    <li className={`${styles.learning} ${editing ? styles.learningEditing : ""}`} data-testid="memory-card">
      <div className={styles.learningHeader}>
        <div className={styles.learningMeta}>
          <span className={styles.learningDate}>
            {edited ? `Updated ${formatDate(memory.updatedAt)}` : `Saved ${formatDate(memory.createdAt)}`}
          </span>
        </div>

        <div className={styles.learningActions}>
          {editing ? (
            <Button variant="ghost" size="sm" iconOnly icon={<X size={15} />} onClick={() => setEditing(false)} title="Cancel" />
          ) : (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              icon={<Pencil size={15} />}
              onClick={startEditing}
              title="Edit memory"
              disabled={isDeleting}
            />
          )}
          <Button
            variant="ghost"
            size="sm"
            iconOnly
            icon={<Trash2 size={15} />}
            loading={isDeleting}
            disabled={isSaving}
            onClick={() => setIsConfirmOpen(true)}
            title="Forget memory"
            className={styles.deleteButton}
          />
        </div>
      </div>

      {editing ? (
        <div className={styles.editBody}>
          <label className={styles.editLabel} htmlFor={`memory-content-${memory.id}`}>
            What agents should remember
          </label>
          <Textarea
            id={`memory-content-${memory.id}`}
            rows={3}
            maxLength={MAX_MEMORY_LENGTH}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
          <div className={styles.editActions}>
            <Button variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
            <Button variant="primary" icon={<Check size={15} />} loading={isSaving} disabled={!draft.trim()} onClick={save}>
              {isSaving ? "Saving…" : "Save"}
            </Button>
          </div>
        </div>
      ) : (
        <p className={styles.learningContent}>{memory.content}</p>
      )}

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Forget this memory?"
        description="Agents will no longer recall it. This cannot be undone."
        confirmLabel="Forget"
        cancelLabel="Cancel"
        onConfirm={() => {
          setIsConfirmOpen(false);
          onDelete();
        }}
        onCancel={() => setIsConfirmOpen(false)}
      />
    </li>
  );
}
