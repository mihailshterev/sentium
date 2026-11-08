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

/** MCP tools are opt-in: "all tools" (null) never includes them, so they are only offered for explicit selection. */
const isMcp = (tool: ToolDescriptor) => Boolean(tool.server);

const groupLabel = (tool: ToolDescriptor) =>
  isMcp(tool) ? `mcp · ${tool.server}` : (GROUPS.find((g) => g.resource === tool.resource)?.label ?? "other");
const groupOrder = (tool: ToolDescriptor) => {
  if (isMcp(tool)) return GROUPS.length + 1;
  const i = GROUPS.findIndex((g) => g.resource === tool.resource);
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

  const builtIn = useMemo(() => tools.filter((t) => !isMcp(t)), [tools]);
  const mcpCount = tools.length - builtIn.length;

  const groups = useMemo(() => {
    const map = new Map<string, ToolDescriptor[]>();
    for (const tool of tools) {
      const key = groupLabel(tool);
      map.set(key, [...(map.get(key) ?? []), tool]);
    }
    return [...map.entries()].sort(([la, a], [lb, b]) => groupOrder(a[0]) - groupOrder(b[0]) || la.localeCompare(lb));
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
    onChange(all ? null : builtIn.map((t) => t.name));
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
          {isLoading ? "loading…" : allTools ? `all ${builtIn.length}` : `${selected.size} of ${tools.length}`}
        </span>
      </div>

      {allTools ? (
        <p className={styles.hint}>
          The agent can use every built-in tool, including ones added later.
          {mcpCount > 0 && " MCP tools are never included automatically - choose “Selected” to add them."}
        </p>
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
                            {isMcp(tool) && <span className={styles.tagExternal}>mcp</span>}
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
