import { SlidersHorizontal } from "lucide-react";
import type {
  SettingControl,
  SettingControlSchema,
  SettingDefinition,
  SettingSchema,
  SettingsSection,
  SettingsSectionSchema,
  SettingValue,
} from "../types/settings";
import { SECTION_PRESENTATION, SETTING_PRESENTATION, type SettingPresentation } from "./settings-presentation";

const DEFAULT_TEXTAREA_ROWS = 4;

const stepDecimals = (step: number) => (String(step).split(".")[1] ?? "").length;

const roundToStep = (step: number) => (value: unknown) => Number(Number(value).toFixed(stepDecimals(step)));

/** `HH:mm:ss` from the server to the `HH:mm` of a time input; null and empty mean "not set". */
const toTimeInput = (value: unknown) => (typeof value === "string" && value ? value.slice(0, 5) : "");
const fromTimeInput = (value: SettingValue) => (value ? `${String(value)}:00` : null);

const nullToEmpty = (value: unknown) => (typeof value === "string" ? value : "");
const emptyToNull = (value: SettingValue) => (value === "" ? null : value);

/** Merges the server's schema with the client-only presentation into what the Settings page renders. */
export function buildSections(schema: SettingsSectionSchema[]): SettingsSection[] {
  return schema.map((section) => {
    const presentation = SECTION_PRESENTATION[section.id];
    return {
      id: section.id,
      scope: section.scope,
      group: section.key,
      title: section.title,
      description: section.description,
      icon: presentation?.icon ?? SlidersHorizontal,
      note: presentation?.note,
      settings: section.settings.map((setting) => buildDefinition(section.key, setting)),
    };
  });
}

function buildDefinition(group: string, setting: SettingSchema): SettingDefinition {
  const presentation = SETTING_PRESENTATION[setting.id] ?? {};
  const { fromServer, toServer } = transforms(setting);

  return {
    id: setting.id,
    group,
    field: setting.field,
    title: setting.title,
    description: setting.description,
    keywords: setting.keywords,
    control: buildControl(setting, presentation),
    defaultValue: fromServer ? fromServer(setting.defaultValue) : (setting.defaultValue as SettingValue),
    fromServer,
    toServer,
    validate: presentation.validate,
    testId: presentation.testId,
  };
}

function buildControl(setting: SettingSchema, presentation: SettingPresentation): SettingControl {
  const control: SettingControlSchema = setting.control;

  switch (control.kind) {
    case "switch":
    case "voice":
      return { kind: control.kind };

    case "number":
      return { kind: "number", min: control.min ?? undefined, max: control.max ?? undefined, unit: control.unit ?? "" };

    case "range": {
      const step = control.step ?? 1;
      return {
        kind: "range",
        min: control.min ?? 0,
        max: control.max ?? 1,
        step,
        minLabel: control.minLabel ?? "",
        maxLabel: control.maxLabel ?? "",
        format: presentation.format ?? ((value) => value.toFixed(stepDecimals(step))),
        explain: presentation.explain,
      };
    }

    case "textarea":
      return {
        kind: "textarea",
        maxLength: control.maxLength ?? Number.MAX_SAFE_INTEGER,
        rows: control.rows ?? DEFAULT_TEXTAREA_ROWS,
        placeholder: control.placeholder ?? "",
      };

    case "time":
      return { kind: "time", required: control.required };

    case "timezone":
      return { kind: "timezone", inherit: setting.nullable };

    case "model":
      return { kind: "model", emptyLabel: control.emptyLabel ?? undefined };
  }
}

function transforms(setting: SettingSchema): Pick<SettingDefinition, "fromServer" | "toServer"> {
  const { kind, step } = setting.control;

  if (kind === "time") {
    return { fromServer: toTimeInput, toServer: fromTimeInput };
  }
  if (kind === "range" && step) {
    return { fromServer: roundToStep(step), toServer: roundToStep(step) };
  }
  if (setting.nullable) {
    return { fromServer: nullToEmpty, toServer: emptyToNull };
  }
  return {};
}
