import type { LucideIcon } from "lucide-react";

/** A Registry settings group: one `/registry/settings/{key}` endpoint. The server's schema lists them. */
export type SettingsKey = string;

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

/** `GET /registry/settings/schema`: built on the server from the settings contracts' attributes. */
export interface SettingsSectionSchema {
  id: string;
  key: SettingsKey;
  scope: SettingScope;
  title: string;
  description: string;
  settings: SettingSchema[];
}

export interface SettingSchema {
  /** `{key}.{field}`. */
  id: string;
  field: string;
  title: string;
  description: string;
  keywords: string[];
  control: SettingControlSchema;
  /** Null is a meaningful value ("inherit", "use the default"); the controls show it as empty. */
  nullable: boolean;
  /** In server form, i.e. before `fromServer`. */
  defaultValue: unknown;
}

export interface SettingControlSchema {
  kind: SettingControl["kind"];
  required: boolean;
  min?: number | null;
  /** Missing on a number means no upper bound. */
  max?: number | null;
  step?: number | null;
  unit?: string | null;
  minLabel?: string | null;
  maxLabel?: string | null;
  maxLength?: number | null;
  rows?: number | null;
  placeholder?: string | null;
  emptyLabel?: string | null;
}

export type SettingControl =
  | { kind: "switch" }
  | { kind: "number"; min?: number; max?: number; unit: string }
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
  /** A Voice the speech server offers; the empty choice is the server's default Voice. */
  | { kind: "voice" }
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
