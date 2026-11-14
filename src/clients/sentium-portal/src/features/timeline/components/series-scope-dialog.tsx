import { Repeat } from "lucide-react";
import styles from "../timeline.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import type { EditScope } from "../types/timeline";

interface SeriesScopeDialogProps {
  action: "edit" | "delete";
  title: string;
  onChoose: (scope: EditScope) => void;
  onCancel: () => void;
}

/**
 * Asks whether an edit or delete of a repeating Entry applies to this occurrence only or to it and every later one.
 * Occurrences already done or past are never deleted either way.
 */
export function SeriesScopeDialog({ action, title, onChoose, onCancel }: SeriesScopeDialogProps) {
  const verb = action === "delete" ? "Delete" : "Save changes to";
  return (
    <Dialog open onClose={onCancel} title={`${verb} a repeating entry`} icon={<Repeat />} size="sm">
      <p className={styles.scopeText}>
        “{title}” repeats.{" "}
        {action === "delete"
          ? "Delete only this occurrence, or this one and every later one that is not done or past?"
          : "Change only this occurrence, or this one and every later one?"}
      </p>
      <div className={styles.formActions}>
        <Button variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <span className={styles.formSpacer} />
        <Button onClick={() => onChoose("One")}>Only this one</Button>
        <Button variant={action === "delete" ? "danger" : "primary"} onClick={() => onChoose("Following")}>
          This and all following
        </Button>
      </div>
    </Dialog>
  );
}
