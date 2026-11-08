import { useId, useMemo } from "react";
import styles from "./tool-picker.module.css";
import type { ToolDescriptor } from "../types/agents";

interface ToolPickerProps {
  id?: string;
  tools: ToolDescriptor[];
  value: string[] | null;
  onChange: (value: string[] | null) => void;
  isLoading?: boolean;
  disabled?: boolean;
}

const GROUPS: { resource: string; label: string }[] = [
  { resource: "VectorDb", label: "knowledge" },
  { resource: "Memory", label: "memory" },
  { resource: "File", label: "files" },
  { resource: "Code", label: "code & scheduling" },
  { resource: "Inventory", label: "inventory" },
  { resource: "Timeline", label: "timeline" },
  { resource: "External", label: "external" },
];

const groupLabel = (resource: string) => GROUPS.find((g) => g.resource === resource)?.label ?? "other";
const groupOrder = (resource: string) => {
  const i = GROUPS.findIndex((g) => g.resource === resource);
  return i < 0 ? GROUPS.length : i;
};

const firstSentence = (text: string) => {
  const flat = text.replace(/\s+/g, " ").trim();
  const end = flat.search(/[.!?](\s|$)/);
  return end > 0 ? flat.slice(0, end + 1) : flat;
};

export function ToolPicker({ id, tools, value, onChange, isLoading = false, disabled = false }: ToolPickerProps) {
  const fallbackId = useId();
  const baseId = id ?? fallbackId;
  const allTools = value === null;
  const selected = useMemo(() => new Set(value ?? []), [value]);

  const groups = useMemo(() => {
    const map = new Map<string, ToolDescriptor[]>();
    for (const tool of tools) {
      const key = groupLabel(tool.resource);
      map.set(key, [...(map.get(key) ?? []), tool]);
    }
    return [...map.entries()].sort(([, a], [, b]) => groupOrder(a[0].resource) - groupOrder(b[0].resource));
  }, [tools]);

  const ordered = (names: Set<string>) => tools.map((t) => t.name).filter((n) => names.has(n));

  const toggle = (name: string, on: boolean) => {
    const next = new Set(selected);
    if (on) next.add(name);
    else next.delete(name);
    onChange(ordered(next));
  };

  const toggleGroup = (members: ToolDescriptor[], on: boolean) => {
    const next = new Set(selected);
    for (const tool of members) {
      if (on) next.add(tool.name);
      else next.delete(tool.name);
    }
    onChange(ordered(next));
  };

  const chooseMode = (all: boolean) => {
    if (all === allTools) return;
    onChange(all ? null : tools.map((t) => t.name));
  };

  return (
    <div className={styles.picker} id={baseId}>
      <div className={styles.modeRow}>
        <div className={styles.segmented} role="group" aria-label="Tool access">
          <button
            type="button"
            className={styles.segment}
            aria-pressed={allTools}
            onClick={() => chooseMode(true)}
            disabled={disabled}
          >
            All tools
          </button>
          <button
            type="button"
            className={styles.segment}
            aria-pressed={!allTools}
            onClick={() => chooseMode(false)}
            disabled={disabled || tools.length === 0}
          >
            Selected
          </button>
        </div>
        <span className={styles.count} aria-live="polite">
          {isLoading ? "loading…" : allTools ? `all ${tools.length}` : `${selected.size} of ${tools.length}`}
        </span>
      </div>

      {allTools ? (
        <p className={styles.hint}>The agent can use every tool, including ones added later.</p>
      ) : (
        <div className={styles.groups}>
          {selected.size === 0 && (
            <p className={styles.hint}>No tools: the agent can only answer from its instructions and skills.</p>
          )}
          {groups.map(([label, members]) => {
            const chosen = members.filter((t) => selected.has(t.name)).length;
            const groupId = `${baseId}-${label.replace(/\W+/g, "-")}`;
            return (
              <fieldset key={label} className={styles.group} aria-labelledby={groupId} disabled={disabled}>
                <div className={styles.groupHead}>
                  <span id={groupId} className={styles.groupLabel}>
                    {label}
                  </span>
                  <span className={styles.groupRule} aria-hidden="true" />
                  <button
                    type="button"
                    className={styles.groupToggle}
                    onClick={() => toggleGroup(members, chosen < members.length)}
                  >
                    {chosen < members.length ? "select all" : "clear"}
                  </button>
                </div>
                <ul className={styles.list}>
                  {members.map((tool) => {
                    const inputId = `${baseId}-${tool.name}`;
                    return (
                      <li key={tool.name}>
                        <label className={styles.row} htmlFor={inputId}>
                          <input
                            id={inputId}
                            type="checkbox"
                            className={styles.check}
                            checked={selected.has(tool.name)}
                            onChange={(e) => toggle(tool.name, e.target.checked)}
                          />
                          <span className={styles.rowMain}>
                            <span className={styles.name}>{tool.name}</span>
                            <span className={styles.desc}>{firstSentence(tool.description)}</span>
                          </span>
                          <span className={styles.tags}>
                            {tool.requiresApproval && <span className={styles.tagApproval}>approval</span>}
                            <span className={styles.tag}>{tool.action.toLowerCase()}</span>
                          </span>
                        </label>
                      </li>
                    );
                  })}
                </ul>
              </fieldset>
            );
          })}
        </div>
      )}
    </div>
  );
}
