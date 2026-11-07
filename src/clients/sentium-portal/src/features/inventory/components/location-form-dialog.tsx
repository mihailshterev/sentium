import { useMemo } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { MapPin } from "lucide-react";
import styles from "../inventory.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import {
  LIMITS,
  locationFormSchema,
  toLocationForm,
  toLocationPayload,
  type LocationFormData,
} from "../schemas/inventory.form";
import type { InventoryLocation, SaveLocationPayload } from "../types/inventory";
import { descendantsOf, flattenForSelect } from "../utils/location-tree";

interface LocationFormDialogProps {
  locations: InventoryLocation[];
  location?: InventoryLocation | null;
  parentId?: string | null;
  onSubmit: (payload: SaveLocationPayload) => void;
  onCancel: () => void;
  isPending: boolean;
  error?: string | null;
}

export function LocationFormDialog({
  locations,
  location,
  parentId,
  onSubmit,
  onCancel,
  isPending,
  error,
}: LocationFormDialogProps) {
  const editing = !!location;
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<LocationFormData>({
    resolver: zodResolver(locationFormSchema),
    defaultValues: toLocationForm(location, parentId),
  });

  const parentOptions = useMemo(
    () => flattenForSelect(locations, location ? descendantsOf(locations, location.id) : new Set()),
    [locations, location],
  );

  const title = editing ? `Edit ${location.name}` : "New location";
  const description = useWatch({ control, name: "description" }) ?? "";
  const accessNotes = useWatch({ control, name: "accessNotes" }) ?? "";

  return (
    <Dialog open onClose={onCancel} title={title} icon={<MapPin />} size="md">
      <form onSubmit={handleSubmit((data) => onSubmit(toLocationPayload(data)))} className={styles.form}>
        <FormField id="loc-name" label="Name" error={errors.name?.message}>
          <Input id="loc-name" placeholder="e.g. Garage, Shelf B" invalid={!!errors.name} {...register("name")} />
        </FormField>

        <FormField
          id="loc-parent"
          label="Inside"
          hint="Leave empty for a top-level place such as a home or a storage unit."
        >
          <Select id="loc-parent" {...register("parentLocationId")}>
            <option value="">— Top level —</option>
            {parentOptions.map((o) => (
              <option key={o.id} value={o.id}>
                {"  ".repeat(o.depth)}
                {o.label.trim()}
              </option>
            ))}
          </Select>
        </FormField>

        <FormField
          id="loc-desc"
          label="Description"
          error={errors.description?.message}
          charCount={{ current: description.length, max: LIMITS.locationText }}
        >
          <Textarea id="loc-desc" rows={2} placeholder="What is kept here?" {...register("description")} />
        </FormField>

        <FormField
          id="loc-access"
          label="Access notes"
          hint="How to get in (keys, codes, quirks). Never shared with assistants."
          error={errors.accessNotes?.message}
          charCount={{ current: accessNotes.length, max: LIMITS.locationText }}
        >
          <Textarea id="loc-access" rows={2} {...register("accessNotes")} />
        </FormField>

        {error && <StatusMessage variant="error" message={error} />}

        <div className={styles.formActions}>
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={isPending}>
            {editing ? "Save" : "Create location"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
