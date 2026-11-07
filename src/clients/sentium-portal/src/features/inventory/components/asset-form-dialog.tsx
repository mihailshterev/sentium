import { useId, useMemo } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Package } from "lucide-react";
import styles from "../inventory.module.css";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Input, Select, SwitchRow, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { StatusMessage } from "@/shared/ui/status-message";
import { assetFormSchema, LIMITS, toAssetForm, toAssetPayload, type AssetFormData } from "../schemas/inventory.form";
import type { Asset, InventoryLocation, SaveAssetPayload } from "../types/inventory";
import { flattenForSelect } from "../utils/location-tree";
import { TagInput } from "./tag-input";

interface AssetFormDialogProps {
  locations: InventoryLocation[];
  asset?: Asset | null;
  defaultLocationId?: string | null;
  categories: string[];
  tagSuggestions: string[];
  onSubmit: (payload: SaveAssetPayload) => void;
  onCancel: () => void;
  isPending: boolean;
  error?: string | null;
}

export function AssetFormDialog({
  locations,
  asset,
  defaultLocationId,
  categories,
  tagSuggestions,
  onSubmit,
  onCancel,
  isPending,
  error,
}: AssetFormDialogProps) {
  const categoryListId = useId();
  const editing = !!asset;
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<AssetFormData>({
    resolver: zodResolver(assetFormSchema),
    defaultValues: toAssetForm(asset, defaultLocationId),
  });

  const locationOptions = useMemo(() => flattenForSelect(locations), [locations]);
  const shared = useWatch({ control, name: "isAgentAccessible" });
  const agentNotes = useWatch({ control, name: "agentInstructions" }) ?? "";

  return (
    <Dialog
      open
      onClose={onCancel}
      title={editing ? `Edit ${asset.displayName}` : "New asset"}
      icon={<Package />}
      size="lg"
    >
      <form onSubmit={handleSubmit((data) => onSubmit(toAssetPayload(data)))} className={styles.form}>
        <div className={styles.formGrid}>
          <FormField id="asset-name" label="Name" error={errors.displayName?.message}>
            <Input
              id="asset-name"
              placeholder="e.g. Cordless drill"
              invalid={!!errors.displayName}
              {...register("displayName")}
            />
          </FormField>
          <FormField id="asset-location" label="Kept at" error={errors.locationId?.message}>
            <Select id="asset-location" invalid={!!errors.locationId} {...register("locationId")}>
              <option value="" disabled>
                Choose a location…
              </option>
              {locationOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.path}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField id="asset-category" label="Category" error={errors.category?.message}>
            <Input id="asset-category" list={categoryListId} placeholder="e.g. Tools" {...register("category")} />
            <datalist id={categoryListId}>
              {categories.map((c) => (
                <option key={c} value={c} />
              ))}
            </datalist>
          </FormField>
          <FormField id="asset-tags" label="Tags" error={errors.tags?.message ?? errors.tags?.[0]?.message}>
            <Controller
              control={control}
              name="tags"
              render={({ field }) => (
                <TagInput
                  id="asset-tags"
                  value={field.value}
                  onChange={field.onChange}
                  suggestions={tagSuggestions}
                  maxTags={LIMITS.tags}
                  maxLength={LIMITS.tag}
                />
              )}
            />
          </FormField>
        </div>

        <fieldset className={styles.fieldset}>
          <legend>Identification</legend>
          <div className={styles.formGrid3}>
            <FormField id="asset-manufacturer" label="Manufacturer">
              <Input id="asset-manufacturer" {...register("manufacturer")} />
            </FormField>
            <FormField id="asset-model" label="Model">
              <Input id="asset-model" {...register("modelNumber")} />
            </FormField>
            <FormField id="asset-serial" label="Serial number">
              <Input id="asset-serial" {...register("serialNumber")} />
            </FormField>
          </div>
          <FormField id="asset-description" label="Physical description" error={errors.physicalDescription?.message}>
            <Textarea
              id="asset-description"
              rows={2}
              placeholder="Colour, size, distinguishing marks…"
              {...register("physicalDescription")}
            />
          </FormField>
        </fieldset>

        <fieldset className={styles.fieldset}>
          <legend>Lifecycle</legend>
          <div className={styles.formGrid}>
            <FormField id="asset-purchased" label="Purchased" error={errors.purchaseDate?.message}>
              <Input id="asset-purchased" type="date" {...register("purchaseDate")} />
            </FormField>
            <FormField id="asset-serviced" label="Last serviced" error={errors.lastServicedDate?.message}>
              <Input
                id="asset-serviced"
                type="date"
                invalid={!!errors.lastServicedDate}
                {...register("lastServicedDate")}
              />
            </FormField>
          </div>
          <FormField id="asset-warranty" label="Warranty" error={errors.warrantyInfo?.message}>
            <Textarea
              id="asset-warranty"
              rows={2}
              placeholder="Provider, expiry, claim details…"
              {...register("warrantyInfo")}
            />
          </FormField>
          <FormField id="asset-instructions" label="Usage and care" error={errors.instructions?.message}>
            <Textarea id="asset-instructions" rows={3} {...register("instructions")} />
          </FormField>
        </fieldset>

        <fieldset className={styles.fieldset}>
          <legend>Assistants</legend>
          <Controller
            control={control}
            name="isAgentAccessible"
            render={({ field }) => (
              <SwitchRow
                label="Share with assistants"
                description="Assistants can find this item and answer questions about it. Turn off to keep it private."
                checked={field.value}
                onChange={(e) => field.onChange(e.target.checked)}
              />
            )}
          />
          {shared && (
            <FormField
              id="asset-agent-notes"
              label="Notes for assistants"
              hint="Context assistants should know, e.g. 'Only adults use this.'"
              error={errors.agentInstructions?.message}
              charCount={{ current: agentNotes.length, max: LIMITS.agentInstructions }}
            >
              <Textarea id="asset-agent-notes" rows={2} {...register("agentInstructions")} />
            </FormField>
          )}
        </fieldset>

        {error && <StatusMessage variant="error" message={error} />}

        <div className={styles.formActions}>
          <Button variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={isPending}>
            {editing ? "Save" : "Create asset"}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
