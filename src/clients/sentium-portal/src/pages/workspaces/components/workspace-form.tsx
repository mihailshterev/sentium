import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { X, Loader2 } from "lucide-react";
import styles from "../workspaces.module.scss";
import { workspaceFormSchema, type WorkspaceFormData } from "../../../schemas/workspace.form";

interface WorkspaceFormProps {
  initial?: { name: string; description: string };
  onSubmit: (name: string, description: string) => void;
  onCancel: () => void;
  isPending: boolean;
  title: string;
}

const WorkspaceForm = ({ initial, onSubmit, onCancel, isPending, title }: WorkspaceFormProps) => {
  const { register, handleSubmit } = useForm<WorkspaceFormData>({
    resolver: zodResolver(workspaceFormSchema),
    defaultValues: { name: initial?.name ?? "", description: initial?.description ?? "" },
  });

  const handleFormSubmit = (data: WorkspaceFormData) => {
    onSubmit(data.name, data.description);
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
        <form onSubmit={handleSubmit(handleFormSubmit)} className={styles.modalForm}>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-name">
              Name
            </label>
            <input
              id="ws-name"
              className={styles.input}
              placeholder="e.g. incident-2026"
              autoFocus
              {...register("name")}
            />
          </div>
          <div className={styles.formGroup}>
            <label className={styles.label} htmlFor="ws-desc">
              Description
            </label>
            <textarea
              id="ws-desc"
              className={`${styles.input} ${styles.textarea}`}
              placeholder="Optional description…"
              rows={3}
              {...register("description")}
            />
          </div>
          <div className={styles.modalActions}>
            <button type="button" className={styles.cancelButton} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className={styles.submitButton} disabled={isPending}>
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
