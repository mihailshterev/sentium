import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Check, Trash2, X } from "lucide-react";
import styles from "./confirm-dialog.module.scss";

export type ConfirmDialogVariant = "danger" | "safe";

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: ConfirmDialogVariant;
  /**
   * When provided, the user must type this word exactly before the
   * confirm button becomes enabled — useful for destructive bulk actions.
   */
  confirmWord?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

const ConfirmDialog = ({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel = "Cancel",
  variant = "danger",
  confirmWord,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) => {
  const [typedValue, setTypedValue] = useState("");
  const overlayRef = useRef<HTMLDivElement>(null);

  const isSafe = variant === "safe";
  const needsTyped = Boolean(confirmWord);
  const isConfirmEnabled = needsTyped ? typedValue === confirmWord : true;
  const defaultConfirmLabel = isSafe ? "Confirm" : "Delete";

  const handleConfirm = () => {
    setTypedValue("");
    onConfirm();
  };

  const handleCancel = () => {
    setTypedValue("");
    onCancel();
  };

  useEffect(() => {
    if (!open) {
      return;
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        handleCancel();
      }
      if (e.key === "Enter" && isConfirmEnabled) {
        e.preventDefault();
        e.stopPropagation();
        handleConfirm();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open, isConfirmEnabled]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!open) {
    return null;
  }

  return (
    <div
      ref={overlayRef}
      className={styles.overlay}
      onClick={(e) => e.target === overlayRef.current && handleCancel()}
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
    >
      <div className={`${styles.dialog} ${isSafe ? styles.dialogSafe : styles.dialogDanger}`}>
        <div className={styles.body}>
          <div className={styles.iconRow}>
            <div className={`${styles.iconWrap} ${isSafe ? styles.iconSafe : styles.iconDanger}`}>
              {isSafe ? <Check size={18} /> : <AlertTriangle size={18} />}
            </div>
            <div className={styles.titles}>
              <p id="confirm-dialog-title" className={styles.title}>
                {title}
              </p>
              <p className={styles.desc}>{description}</p>
            </div>
          </div>

          {needsTyped && (
            <div className={styles.inputRow}>
              <label className={styles.inputLabel}>
                Type{" "}
                <span className={`${styles.confirmWord} ${isSafe ? styles.confirmWordSafe : styles.confirmWordDanger}`}>
                  {confirmWord}
                </span>{" "}
                to confirm
              </label>
              <input
                autoFocus
                className={`${styles.input} ${isSafe ? styles.inputSafe : styles.inputDanger}`}
                type="text"
                autoComplete="off"
                spellCheck={false}
                value={typedValue}
                onChange={(e) => setTypedValue(e.target.value)}
              />
            </div>
          )}
        </div>

        <div className={styles.footer}>
          <button className={styles.btnCancel} onClick={handleCancel}>
            <X size={14} />
            {cancelLabel}
          </button>
          <button
            className={`${styles.btnConfirm} ${isSafe ? styles.btnConfirmSafe : styles.btnConfirmDanger}`}
            disabled={!isConfirmEnabled}
            onClick={handleConfirm}
          >
            {isSafe ? <Check size={14} /> : <Trash2 size={14} />}
            {confirmLabel ?? defaultConfirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ConfirmDialog;
