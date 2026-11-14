import type { LucideIcon } from "lucide-react";

/** A Registry settings group: one `/registry/settings/{key}` endpoint. */
export type SettingsKey = "harness" | "calendar-user" | "inference" | "calendar" | "pdp" | "mcp" | "vigil";

export interface SettingsEnvelope<T> {
  key: string;
  value: T;
  updatedAt: string;
  updatedBy: string | null;
}

export type SettingsGroupValue = Record<string, unknown>;

/** Personal settings only affect the caller; household settings apply to everyone and need a Sovereign. */
export type SettingScope = "personal" | "household";

export type SettingValue = string | number | boolean;

export type SettingControl =
  | { kind: "switch" }
  | { kind: "number"; min: number; max?: number; unit: string }
  | {
      kind: "range";
      min: number;
      max: number;
      step: number;
      minLabel: string;
      maxLabel: string;
      format: (value: number) => string;
      /** Extra line under the slider that explains the selected value. */
      explain?: (value: number) => string;
    }
  | { kind: "textarea"; maxLength: number; rows: number; placeholder: string }
  | { kind: "time"; required: boolean }
  /** `inherit`: an empty option that falls back to the household zone. */
  | { kind: "timezone"; inherit: boolean }
  /** `emptyLabel`: offer an empty choice with this label, e.g. "Default model". */
  | { kind: "model"; emptyLabel?: string };

export interface SettingDefinition {
  /** `{group}.{field}`, unique across the catalog. */
  id: string;
  group: SettingsKey;
  field: string;
  title: string;
  description: string;
  keywords?: string[];
  control: SettingControl;
  /** In control form, i.e. after `fromServer`. */
  defaultValue: SettingValue;
  fromServer?: (value: unknown) => SettingValue;
  toServer?: (value: SettingValue) => unknown;
  /** Rules that span settings; per-control bounds are checked generically. */
  validate?: (value: SettingValue, valueOf: (id: string) => SettingValue) => string | null;
  testId?: string;
}

export interface SettingsSection {
  /** URL slug: `/settings/{id}`. */
  id: string;
  scope: SettingScope;
  group: SettingsKey;
  title: string;
  description: string;
  icon: LucideIcon;
  settings: SettingDefinition[];
  /** A pointer to a related control that lives elsewhere. */
  note?: { text: string; linkLabel: string; to: string };
}
