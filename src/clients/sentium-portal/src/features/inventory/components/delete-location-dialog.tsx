import { useMemo, useState } from "react";
import { Trash2 } from "lucide-react";
import styles from "../inventory.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Select } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import type { InventoryLocation } from "../types/inventory";
import { descendantsOf, flattenForSelect } from "../utils/location-tree";

interface DeleteLocationDialogProps {
  location: InventoryLocation;
  locations: InventoryLocation[];
  onConfirm: (moveContentsTo: string | null) => void;
  onCancel: () => void;
  isPending: boolean;
  error?: string | null;
}

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;

export function DeleteLocationDialog({
  location,
  locations,
  onConfirm,
  onCancel,
  isPending,
  error,
}: DeleteLocationDialogProps) {
  const isEmpty = location.childCount === 0 && location.assetCount === 0;

  const targets = useMemo(
    () => flattenForSelect(locations, descendantsOf(locations, location.id)),
    [locations, location.id],
  );
  const [target, setTarget] = useState<string>(location.parentLocationId ?? "");

  const contents = [
    location.childCount > 0 ? plural(location.childCount, "sub-location") : null,
    location.assetCount > 0 ? plural(location.assetCount, "item") : null,
  ]
    .filter(Boolean)
    .join(" and ");

  return (
    <Dialog
      open
      onClose={onCancel}
      title={`Delete ${location.name}?`}
      icon={<Trash2 />}
      iconTone="danger"
      size="sm"
      footer={
        <>
          <Button variant="ghost" onClick={onCancel} data-testid="confirm-dialog-cancel">
            Cancel
          </Button>
          <Button
            variant="danger"
            loading={isPending}
            disabled={!isEmpty && !target}
            onClick={() => onConfirm(isEmpty ? null : target)}
            data-testid="confirm-dialog-confirm"
          >
            {isEmpty ? "Delete" : "Move & delete"}
          </Button>
        </>
      }
    >
      <div className={styles.form}>
        {isEmpty ? (
          <p className={styles.dialogText}>This location is empty. Deleting it cannot be undone.</p>
        ) : (
          <>
            <p className={styles.dialogText}>
              <strong>{location.path}</strong> still holds {contents}. They will be moved before the location is
              deleted.
            </p>
            <FormField id="delete-move-to" label="Move its contents to">
              <Select id="delete-move-to" value={target} onChange={(e) => setTarget(e.target.value)}>
                <option value="" disabled>
                  Choose a location…
                </option>
                {targets.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.path}
                  </option>
                ))}
              </Select>
            </FormField>
          </>
        )}
        {error && <StatusMessage variant="error" message={error} />}
      </div>
    </Dialog>
  );
}
