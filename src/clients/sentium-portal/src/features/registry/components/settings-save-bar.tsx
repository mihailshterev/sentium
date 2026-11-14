import { CheckCircle, X } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { StatusMessage } from "@/shared/ui/status-message";
import styles from "../settings.module.css";

export type SaveOutcome = { kind: "saved" } | { kind: "failed"; sections: string[] } | null;

interface SettingsSaveBarProps {
  dirtyCount: number;
  invalidCount: number;
  isSaving: boolean;
  outcome: SaveOutcome;
  onSave: () => void;
  onDiscard: () => void;
}

/** Appears once there is something to save or report, like the contextual save bars of most admin consoles. */
export function SettingsSaveBar({
  dirtyCount,
  invalidCount,
  isSaving,
  outcome,
  onSave,
  onDiscard,
}: SettingsSaveBarProps) {
  if (dirtyCount === 0 && !isSaving && !outcome) {
    return null;
  }

  return (
    <div className={styles.saveBar} data-dirty={dirtyCount > 0 || undefined} role="region" aria-label="Unsaved changes">
      <div className={styles.saveStatus} aria-live="polite">
        {outcome?.kind === "failed" ? (
          <StatusMessage
            variant="error"
            icon={<X />}
            message={`Failed to save ${outcome.sections.join(", ")}. Other changes were saved.`}
          />
        ) : outcome?.kind === "saved" && dirtyCount === 0 ? (
          <StatusMessage variant="success" icon={<CheckCircle />} message="Settings successfully saved." />
        ) : invalidCount > 0 ? (
          <span className={styles.saveHint} data-invalid>
            Fix {invalidCount === 1 ? "1 invalid value" : `${invalidCount} invalid values`} to save
          </span>
        ) : (
          <span className={styles.saveHint}>
            {dirtyCount === 1 ? "1 unsaved change" : `${dirtyCount} unsaved changes`}
          </span>
        )}
      </div>
      {dirtyCount > 0 && (
        <div className={styles.saveActions}>
          <Button variant="ghost" onClick={onDiscard} disabled={isSaving}>
            Discard
          </Button>
          <Button
            variant="primary"
            icon={<CheckCircle size={15} />}
            loading={isSaving}
            disabled={invalidCount > 0}
            onClick={onSave}
          >
            {isSaving ? "Saving…" : "Save changes"}
          </Button>
        </div>
      )}
    </div>
  );
}
