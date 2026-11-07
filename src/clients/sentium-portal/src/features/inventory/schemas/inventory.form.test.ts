import { describe, expect, it } from "vitest";
import {
  assetFormSchema,
  locationFormSchema,
  normalizeTag,
  toAssetForm,
  toAssetPayload,
  toLocationPayload,
} from "./inventory.form";

describe("inventory form schemas", () => {
  const validAsset = { ...toAssetForm(null, "loc-1"), displayName: "Drill" };

  it("accepts a minimal asset", () => {
    expect(assetFormSchema.safeParse(validAsset).success).toBe(true);
  });

  it("requires a name and a location", () => {
    const result = assetFormSchema.safeParse({ ...validAsset, displayName: "  ", locationId: "" });

    expect(result.success).toBe(false);
    const paths = result.error!.issues.map((i) => i.path[0]);
    expect(paths).toContain("displayName");
    expect(paths).toContain("locationId");
  });

  it("rejects a service date before the purchase date", () => {
    const result = assetFormSchema.safeParse({
      ...validAsset,
      purchaseDate: "2025-05-01",
      lastServicedDate: "2024-01-01",
    });

    expect(result.success).toBe(false);
    expect(result.error!.issues[0].path).toEqual(["lastServicedDate"]);
  });

  it("limits tags", () => {
    expect(
      assetFormSchema.safeParse({ ...validAsset, tags: Array.from({ length: 21 }, (_, i) => `t${i}`) }).success,
    ).toBe(false);
    expect(assetFormSchema.safeParse({ ...validAsset, tags: ["x".repeat(33)] }).success).toBe(false);
  });

  it("maps the form to a payload, turning blanks into nulls and dropping notes of private assets", () => {
    const payload = toAssetPayload({
      ...validAsset,
      category: "  ",
      manufacturer: " Bosch ",
      purchaseDate: "2024-03-01",
      isAgentAccessible: false,
      agentInstructions: "stale",
      tags: ["power"],
    });

    expect(payload).toMatchObject({
      displayName: "Drill",
      locationId: "loc-1",
      category: null,
      manufacturer: "Bosch",
      purchaseDate: "2024-03-01",
      lastServicedDate: null,
      isAgentAccessible: false,
      agentInstructions: null,
      tags: ["power"],
    });
  });

  it("maps a location form, top level as null parent", () => {
    expect(
      locationFormSchema.safeParse({ name: "", parentLocationId: "", description: "", accessNotes: "" }).success,
    ).toBe(false);
    expect(
      toLocationPayload({ name: " Garage ", parentLocationId: "", description: "", accessNotes: " key " }),
    ).toEqual({
      name: "Garage",
      parentLocationId: null,
      description: null,
      accessNotes: "key",
    });
  });

  it("normalizes tags like the server", () => {
    expect(normalizeTag("  Power   Tools ")).toBe("power tools");
  });
});
