import { useState } from "react";
import { validateSetting } from "../catalog/settings-rules";
import type {
  SettingDefinition,
  SettingsEnvelope,
  SettingsGroupValue,
  SettingsKey,
  SettingValue,
} from "../types/settings";
import type { GroupPatch } from "./useSettingsGroups";

type Envelopes = Partial<Record<SettingsKey, SettingsEnvelope<SettingsGroupValue>>>;

const savedValue = (definition: SettingDefinition, envelopes: Envelopes): SettingValue => {
  const raw = envelopes[definition.group]?.value[definition.field];
  if (raw === undefined) {
    return definition.defaultValue;
  }
  return definition.fromServer ? definition.fromServer(raw) : (raw as SettingValue);
};

/**
 * Unsaved edits on top of the server values. A draft equal to the saved value is dropped, so "dirty" always means
 * "would change something".
 */
export function useSettingsDraft(definitions: SettingDefinition[], envelopes: Envelopes) {
  const [draft, setDraft] = useState<Record<string, SettingValue>>({});

  const saved = Object.fromEntries(definitions.map((d) => [d.id, savedValue(d, envelopes)]));
  const valueOf = (id: string): SettingValue => (id in draft ? draft[id] : saved[id]);

  const errors: Record<string, string> = {};
  for (const definition of definitions) {
    const error = validateSetting(definition, valueOf(definition.id), valueOf);
    if (error) {
      errors[definition.id] = error;
    }
  }

  const setValue = (id: string, value: SettingValue) =>
    setDraft((current) => {
      const next = { ...current };
      if (value === saved[id]) {
        delete next[id];
      } else {
        next[id] = value;
      }
      return next;
    });

  const patches = (): GroupPatch[] => {
    const byGroup = new Map<SettingsKey, SettingsGroupValue>();
    for (const definition of definitions) {
      if (!(definition.id in draft)) {
        continue;
      }
      const value = draft[definition.id];
      const patch = byGroup.get(definition.group) ?? {};
      patch[definition.field] = definition.toServer ? definition.toServer(value) : value;
      byGroup.set(definition.group, patch);
    }
    return [...byGroup].map(([key, patch]) => ({ key, patch }));
  };

  const clearGroups = (keys: SettingsKey[]) => {
    const ids = new Set(definitions.filter((d) => keys.includes(d.group)).map((d) => d.id));
    setDraft((current) => Object.fromEntries(Object.entries(current).filter(([id]) => !ids.has(id))));
  };

  return {
    valueOf,
    setValue,
    isDirty: (id: string) => id in draft,
    dirtyCount: Object.keys(draft).length,
    errors,
    patches,
    clearGroups,
    discard: () => setDraft({}),
  };
}
