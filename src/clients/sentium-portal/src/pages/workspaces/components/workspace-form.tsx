import { useState } from "react";
import { X, Loader2 } from "lucide-react";
import styles from "../workspaces.module.scss";

interface WorkspaceFormProps {
  initial?: { name: string; description: string };
  onSubmit: (name: string, description: string) => void;
  onCancel: () => void;
  isPending: boolean;
  title: string;
}

const WorkspaceForm = ({ initial, onSubmit, onCancel, isPending, title }: WorkspaceFormProps) => {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (name.trim()) {
      onSubmit(name.trim(), description.trim());
    }
  };

  return (
    <div className={styles.modalOverlay} onClick={onCancel}>
      <div className={styles.modal} onClick={(e) => e.stopPropagation()}>
        <div className={styles.modalHeader}>
          <span className={styles.modalTitle}>{title}</span>
          <button className={styles.modalClose} onClick={onCancel}>
            <X size={14} />
          </button>
        </div>
        <form onSubmit={handleSubmit} className={styles.modalForm}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-name">
              Name
            </label>
            <input
              id="ws-name"
              className={styles.input}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. incident-2026"
              autoFocus
              required
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-desc">
              Description
            </label>
            <textarea
              id="ws-desc"
              className={`${styles.input} ${styles.textarea}`}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description…"
              rows={3}
            />
          </div>
          <div className={styles.modalActions}>
            <button type="button" className={styles.cancelButton} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className={styles.submitButton} disabled={!name.trim() || isPending}>
              {isPending ? <Loader2 size={14} className={styles.spin} /> : null}
              {title}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default WorkspaceForm;
