import { useEffect, useEffectEvent, useId, useRef, useState } from "react";
import { AlertTriangle, Check, Trash2 } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input } from "@/shared/ui/form-controls";
import styles from "./confirm-dialog.module.css";

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

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel = "Cancel",
  variant = "danger",
  confirmWord,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const [typedValue, setTypedValue] = useState("");
  const cancelRef = useRef<HTMLButtonElement>(null);
  const inputId = useId();

  const isSafe = variant === "safe";
  const isConfirmEnabled = confirmWord ? typedValue === confirmWord : true;

  const handleConfirm = () => {
    setTypedValue("");
    onConfirm();
  };

  const handleCancel = () => {
    setTypedValue("");
    onCancel();
  };

  // Enter confirms from anywhere in the dialog (Escape is handled by Dialog).
  const onEnter = useEffectEvent((e: KeyboardEvent) => {
    if (e.key === "Enter" && isConfirmEnabled) {
      e.preventDefault();
      e.stopPropagation();
      handleConfirm();
    }
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    const listener = (e: KeyboardEvent) => onEnter(e);
    document.addEventListener("keydown", listener);
    return () => document.removeEventListener("keydown", listener);
  }, [open]);

  return (
    <Dialog
      open={open}
      onClose={handleCancel}
      size="sm"
      title={title}
      description={description}
      icon={isSafe ? <Check /> : <AlertTriangle />}
      iconTone={isSafe ? "success" : "danger"}
      hideCloseButton
      initialFocus={confirmWord ? undefined : cancelRef}
      footer={
        <>
          <Button ref={cancelRef} variant="secondary" onClick={handleCancel} data-testid="confirm-dialog-cancel">
            {cancelLabel}
          </Button>
          <Button
            variant={isSafe ? "primary" : "danger"}
            disabled={!isConfirmEnabled}
            onClick={handleConfirm}
            icon={isSafe ? <Check size={15} /> : <Trash2 size={15} />}
            data-testid="confirm-dialog-confirm"
          >
            {confirmLabel ?? (isSafe ? "Confirm" : "Delete")}
          </Button>
        </>
      }
    >
      {confirmWord && (
        <div className={styles.typed}>
          <label htmlFor={inputId} className={styles.typedLabel}>
            Type <code className={isSafe ? styles.wordSafe : styles.wordDanger}>{confirmWord}</code> to confirm
          </label>
          <Input
            id={inputId}
            autoFocus
            type="text"
            autoComplete="off"
            spellCheck={false}
            value={typedValue}
            onChange={(e) => setTypedValue(e.target.value)}
          />
        </div>
      )}
    </Dialog>
  );
}
