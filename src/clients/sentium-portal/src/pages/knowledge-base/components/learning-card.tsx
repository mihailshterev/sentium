import { useState } from "react";
import { Check, Globe, Loader, Lock, Pencil, Trash2, X } from "lucide-react";
import styles from "../knowledge-base.module.scss";
import ConfirmDialog from "../../../components/ui/confirm-dialog";

interface LearningCardProps {
  learning: {
    id: string;
    agentName: string;
    content: string;
    tags: string;
    capturedAt: string;
    isIngested: boolean;
    isGlobal: boolean;
  };
  isSaving: boolean;
  isDeleting: boolean;
  onSave: (content: string, tags: string) => void;
  onDelete: () => void;
  formatDate: (iso: string) => string;
}

const LearningCard = ({ learning: l, isSaving, isDeleting, onSave, onDelete, formatDate }: LearningCardProps) => {
  const [editing, setEditing] = useState(false);
  const [editContent, setEditContent] = useState(l.content);
  const [editTags, setEditTags] = useState(l.tags);

  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const handleEdit = () => {
    setEditContent(l.content);
    setEditTags(l.tags);
    setEditing(true);
  };

  const handleCancel = () => setEditing(false);

  const handleSave = () => {
    if (!editContent.trim()) {
      return;
    }
    onSave(editContent.trim(), editTags.trim());
    setEditing(false);
  };

  const handleConfirmDelete = () => {
    setIsConfirmOpen(false);
    onDelete();
  };

  return (
    <div
      className={`${styles.learningCard} ${editing ? styles.learningCardEditing : ""} ${l.isGlobal ? styles.learningCardGlobal : ""}`}
    >
      <div className={styles.learningCardHeader}>
        <div className={styles.learningMeta}>
          <span className={styles.agentBadge}>{l.agentName}</span>
          <span className={styles.learningDate}>{formatDate(l.capturedAt)}</span>
          {l.tags &&
            l.tags
              .split(",")
              .filter(Boolean)
              .map((tag) => (
                <span key={tag} className={styles.tag}>
                  {tag.trim()}
                </span>
              ))}
          <span
            className={l.isGlobal ? styles.pillCyan : styles.pillMuted}
            title={l.isGlobal ? "Validated and shared with all users' agents" : "Private to your agents"}
          >
            {l.isGlobal ? <Globe size={10} /> : <Lock size={10} />}
            {l.isGlobal ? "Global" : "Private"}
          </span>
          <span className={l.isIngested ? styles.pillGreen : styles.pillAmber}>
            {l.isIngested ? "Indexed" : "Pending"}
          </span>
        </div>

        <div className={styles.cardActions}>
          {editing ? (
            <button className={`${styles.btnIcon} ${styles.btnIconActive}`} onClick={handleCancel} title="Cancel">
              <X size={13} />
            </button>
          ) : (
            <button className={styles.btnIcon} onClick={handleEdit} title="Edit learning" disabled={isDeleting}>
              <Pencil size={13} />
            </button>
          )}
          <button
            className={`${styles.btnIcon} ${styles.btnIconDanger}`}
            disabled={isDeleting || isSaving}
            onClick={() => setIsConfirmOpen(true)}
            title="Delete learning"
          >
            {isDeleting ? <Loader size={13} className={styles.spin} /> : <Trash2 size={13} />}
          </button>
        </div>
      </div>

      {editing ? (
        <div className={styles.editBody}>
          <p className={styles.editLabel}>Content (markdown)</p>
          <textarea
            className={styles.editTextarea}
            value={editContent}
            onChange={(e) => setEditContent(e.target.value)}
            spellCheck={false}
          />
          <p className={styles.editLabel}>Tags (comma-separated)</p>
          <input
            className={styles.editTagsInput}
            type="text"
            value={editTags}
            onChange={(e) => setEditTags(e.target.value)}
            placeholder="e.g. workflow, memory, agent"
          />
          <div className={styles.editActions}>
            <button className={styles.btnSecondary} onClick={handleCancel}>
              Cancel
            </button>
            <button className={styles.btnPrimary} onClick={handleSave} disabled={isSaving || !editContent.trim()}>
              {isSaving ? <Loader size={12} /> : <Check size={12} />}
              {isSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      ) : (
        <div className={styles.learningContent}>{l.content}</div>
      )}

      <ConfirmDialog
        open={isConfirmOpen}
        variant="danger"
        title="Delete Knowledge Record"
        description="Are you sure you want to delete this captured memory item? This cannot be undone and will remove it from the agent's contextual knowledge base."
        confirmLabel="Delete Item"
        cancelLabel="Cancel"
        onConfirm={handleConfirmDelete}
        onCancel={() => setIsConfirmOpen(false)}
      />
    </div>
  );
};

export default LearningCard;
