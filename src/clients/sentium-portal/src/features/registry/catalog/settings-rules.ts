import type { SettingDefinition, SettingsSection, SettingValue } from "../types/settings";

export function validateSetting(
  definition: SettingDefinition,
  value: SettingValue,
  valueOf: (id: string) => SettingValue,
): string | null {
  const { control } = definition;

  if (control.kind === "number") {
    const number = Number(value);
    if (value === "" || Number.isNaN(number)) {
      return "Enter a number.";
    }
    if (!Number.isInteger(number)) {
      return "Enter a whole number.";
    }
    if (number < control.min || (control.max !== undefined && number > control.max)) {
      return control.max === undefined
        ? `Must be at least ${control.min}.`
        : `Must be between ${control.min} and ${control.max}.`;
    }
  }

  if (control.kind === "textarea" && String(value).length > control.maxLength) {
    return `${String(value).length - control.maxLength} characters over limit.`;
  }

  if (control.kind === "time" && control.required && !value) {
    return "Pick a time.";
  }

  return definition.validate?.(value, valueOf) ?? null;
}

/** Every whitespace-separated term must appear in the setting's or its section's text. */
export function matchesSearch(definition: SettingDefinition, section: SettingsSection, query: string): boolean {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  if (terms.length === 0) {
    return true;
  }
  const haystack = [definition.title, definition.description, section.title, ...(definition.keywords ?? [])]
    .join(" ")
    .toLowerCase();
  return terms.every((term) => haystack.includes(term));
}
