import { z } from "zod";
import type { Asset, InventoryLocation, SaveAssetPayload, SaveLocationPayload } from "../types/inventory";

export const LIMITS = {
  locationName: 100,
  locationText: 1000,
  assetName: 200,
  category: 100,
  description: 2000,
  instructions: 4000,
  manufacturer: 200,
  identifier: 100,
  warranty: 2000,
  agentInstructions: 2000,
  tag: 32,
  tags: 20,
  attachmentBytes: 25 * 1024 * 1024,
} as const;

const optionalDate = z
  .string()
  .refine((v) => v === "" || /^\d{4}-\d{2}-\d{2}$/.test(v), "Use the date picker (YYYY-MM-DD)");

export const locationFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Name is required")
    .max(LIMITS.locationName, `At most ${LIMITS.locationName} characters`),
  parentLocationId: z.string(),
  description: z.string().max(LIMITS.locationText, `At most ${LIMITS.locationText} characters`),
  accessNotes: z.string().max(LIMITS.locationText, `At most ${LIMITS.locationText} characters`),
});

export type LocationFormData = z.infer<typeof locationFormSchema>;

export const assetFormSchema = z
  .object({
    displayName: z
      .string()
      .trim()
      .min(1, "Name is required")
      .max(LIMITS.assetName, `At most ${LIMITS.assetName} characters`),
    locationId: z.string().min(1, "Choose where it is kept"),
    category: z.string().max(LIMITS.category),
    manufacturer: z.string().max(LIMITS.manufacturer),
    modelNumber: z.string().max(LIMITS.identifier),
    serialNumber: z.string().max(LIMITS.identifier),
    physicalDescription: z.string().max(LIMITS.description),
    instructions: z.string().max(LIMITS.instructions),
    warrantyInfo: z.string().max(LIMITS.warranty),
    purchaseDate: optionalDate,
    lastServicedDate: optionalDate,
    isAgentAccessible: z.boolean(),
    agentInstructions: z.string().max(LIMITS.agentInstructions),
    tags: z
      .array(z.string().max(LIMITS.tag, `Tags can be at most ${LIMITS.tag} characters`))
      .max(LIMITS.tags, `At most ${LIMITS.tags} tags`),
  })
  .refine((d) => !d.purchaseDate || !d.lastServicedDate || d.lastServicedDate >= d.purchaseDate, {
    path: ["lastServicedDate"],
    message: "Cannot be before the purchase date",
  });

export type AssetFormData = z.infer<typeof assetFormSchema>;

const orNull = (value: string) => (value.trim() === "" ? null : value.trim());

export function toLocationForm(location?: InventoryLocation | null, parentId?: string | null): LocationFormData {
  return {
    name: location?.name ?? "",
    parentLocationId: location?.parentLocationId ?? parentId ?? "",
    description: location?.description ?? "",
    accessNotes: location?.accessNotes ?? "",
  };
}

export function toLocationPayload(form: LocationFormData): SaveLocationPayload {
  return {
    name: form.name.trim(),
    parentLocationId: form.parentLocationId || null,
    description: orNull(form.description),
    accessNotes: orNull(form.accessNotes),
  };
}

export function toAssetForm(asset?: Asset | null, locationId?: string | null): AssetFormData {
  return {
    displayName: asset?.displayName ?? "",
    locationId: asset?.locationId ?? locationId ?? "",
    category: asset?.category ?? "",
    manufacturer: asset?.manufacturer ?? "",
    modelNumber: asset?.modelNumber ?? "",
    serialNumber: asset?.serialNumber ?? "",
    physicalDescription: asset?.physicalDescription ?? "",
    instructions: asset?.instructions ?? "",
    warrantyInfo: asset?.warrantyInfo ?? "",
    purchaseDate: asset?.purchaseDate ?? "",
    lastServicedDate: asset?.lastServicedDate ?? "",
    isAgentAccessible: asset?.isAgentAccessible ?? true,
    agentInstructions: asset?.agentInstructions ?? "",
    tags: asset?.tags ?? [],
  };
}

export function toAssetPayload(form: AssetFormData): SaveAssetPayload {
  return {
    displayName: form.displayName.trim(),
    locationId: form.locationId,
    category: orNull(form.category),
    manufacturer: orNull(form.manufacturer),
    modelNumber: orNull(form.modelNumber),
    serialNumber: orNull(form.serialNumber),
    physicalDescription: orNull(form.physicalDescription),
    instructions: orNull(form.instructions),
    warrantyInfo: orNull(form.warrantyInfo),
    purchaseDate: form.purchaseDate || null,
    lastServicedDate: form.lastServicedDate || null,
    isAgentAccessible: form.isAgentAccessible,
    agentInstructions: form.isAgentAccessible ? orNull(form.agentInstructions) : null,
    tags: form.tags,
  };
}
