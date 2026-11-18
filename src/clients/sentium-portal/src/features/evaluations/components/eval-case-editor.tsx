import { Plus, Trash2 } from "lucide-react";
import styles from "../evaluations.module.css";
import type { ToolDescriptor } from "@/features/agents/types/agents";
import { Button } from "@/shared/ui/button";
import { Input, Select, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { CHECKS, DEFAULT_QUALITY_THRESHOLD, type CaseDraft } from "../utils/case-draft";
import type { EvalCheckType, EvalTurnRole } from "../types/eval-suites";

interface EvalCaseEditorProps {
  draft: CaseDraft;
  tools: ToolDescriptor[];
  readOnly: boolean;
  onChange: (draft: CaseDraft) => void;
}

function ToolSelect({
  id,
  value,
  tools,
  disabled,
  onChange,
}: {
  id: string;
  value: string;
  tools: ToolDescriptor[];
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const known = value === "" || tools.some((t) => t.name === value);
  return (
    <Select id={id} aria-label="Tool" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
      <option value="">Choose a tool…</option>
      {!known && <option value={value}>{value} (unknown)</option>}
      {tools.map((t) => (
        <option key={t.name} value={t.name}>
          {t.server ? `${t.name} (${t.server})` : t.name}
        </option>
      ))}
    </Select>
  );
}

export function EvalCaseEditor({ draft, tools, readOnly, onChange }: EvalCaseEditorProps) {
  const set = (patch: Partial<CaseDraft>) => onChange({ ...draft, ...patch });
  const idPrefix = `case-${draft.key}`;

  const replaceAt = <T,>(list: T[], index: number, item: T) => list.map((x, i) => (i === index ? item : x));
  const removeAt = <T,>(list: T[], index: number) => list.filter((_, i) => i !== index);

  const toggleCheck = (type: EvalCheckType, on: boolean) =>
    set({ checks: on ? [...draft.checks, { type, threshold: "" }] : draft.checks.filter((c) => c.type !== type) });

  return (
    <div className={styles.section}>
      <div className={styles.sectionGrid}>
        <FormField id={`${idPrefix}-name`} label="Name" hint="Optional; shown in results.">
          <Input
            id={`${idPrefix}-name`}
            value={draft.name}
            maxLength={120}
            disabled={readOnly}
            onChange={(e) => set({ name: e.target.value })}
          />
        </FormField>
      </div>

      <FormField id={`${idPrefix}-prompt`} label="Prompt">
        <Textarea
          id={`${idPrefix}-prompt`}
          rows={4}
          value={draft.prompt}
          disabled={readOnly}
          onChange={(e) => set({ prompt: e.target.value })}
        />
      </FormField>

      <section className={styles.group} aria-label="Prior turns">
        <div className={styles.groupHead}>
          <p className={styles.groupTitle}>Prior turns</p>
          {!readOnly && (
            <Button
              size="sm"
              variant="ghost"
              icon={<Plus size={14} />}
              onClick={() => set({ priorTurns: [...draft.priorTurns, { role: "User", text: "" }] })}
            >
              Add turn
            </Button>
          )}
        </div>
        {draft.priorTurns.length === 0 && <p className={styles.muted}>None: the prompt starts the conversation.</p>}
        {draft.priorTurns.map((turn, i) => (
          <div className={styles.item} key={i}>
            <Select
              aria-label={`Turn ${i + 1} role`}
              value={turn.role}
              disabled={readOnly}
              onChange={(e) =>
                set({ priorTurns: replaceAt(draft.priorTurns, i, { ...turn, role: e.target.value as EvalTurnRole }) })
              }
            >
              <option value="User">User</option>
              <option value="Assistant">Assistant</option>
            </Select>
            <Textarea
              aria-label={`Turn ${i + 1} text`}
              rows={2}
              value={turn.text}
              disabled={readOnly}
              onChange={(e) => set({ priorTurns: replaceAt(draft.priorTurns, i, { ...turn, text: e.target.value }) })}
            />
            {!readOnly && (
              <Button
                iconOnly
                variant="ghost"
                aria-label={`Remove turn ${i + 1}`}
                icon={<Trash2 size={14} />}
                onClick={() => set({ priorTurns: removeAt(draft.priorTurns, i) })}
              />
            )}
          </div>
        ))}
      </section>

      <FormField id={`${idPrefix}-expected`} label="Expected answer" hint="Needed by answer Checks.">
        <Textarea
          id={`${idPrefix}-expected`}
          rows={3}
          value={draft.expectedAnswer}
          disabled={readOnly}
          onChange={(e) => set({ expectedAnswer: e.target.value })}
        />
      </FormField>

      <section className={styles.group} aria-label="Expected tool calls">
        <div className={styles.groupHead}>
          <p className={styles.groupTitle}>Expected tool calls</p>
          {!readOnly && (
            <Button
              size="sm"
              variant="ghost"
              icon={<Plus size={14} />}
              onClick={() => set({ expectedToolCalls: [...draft.expectedToolCalls, { toolName: "", argumentsJson: "" }] })}
            >
              Add tool call
            </Button>
          )}
        </div>
        {draft.expectedToolCalls.map((call, i) => (
          <div className={styles.item} key={i}>
            <ToolSelect
              id={`${idPrefix}-call-${i}`}
              value={call.toolName}
              tools={tools}
              disabled={readOnly}
              onChange={(toolName) => set({ expectedToolCalls: replaceAt(draft.expectedToolCalls, i, { ...call, toolName }) })}
            />
            <Textarea
              aria-label={`Tool call ${i + 1} arguments`}
              mono
              rows={2}
              placeholder='Arguments that must match, e.g. {"query": "milk"} (blank: not checked)'
              value={call.argumentsJson}
              disabled={readOnly}
              onChange={(e) =>
                set({ expectedToolCalls: replaceAt(draft.expectedToolCalls, i, { ...call, argumentsJson: e.target.value }) })
              }
            />
            {!readOnly && (
              <Button
                iconOnly
                variant="ghost"
                aria-label={`Remove tool call ${i + 1}`}
                icon={<Trash2 size={14} />}
                onClick={() => set({ expectedToolCalls: removeAt(draft.expectedToolCalls, i) })}
              />
            )}
          </div>
        ))}
      </section>

      <section className={styles.group} aria-label="Canned tool results">
        <div className={styles.groupHead}>
          <p className={styles.groupTitle}>Canned tool results</p>
          {!readOnly && (
            <Button
              size="sm"
              variant="ghost"
              icon={<Plus size={14} />}
              onClick={() => set({ cannedToolResults: [...draft.cannedToolResults, { toolName: "", result: "" }] })}
            >
              Add result
            </Button>
          )}
        </div>
        <p className={styles.muted}>Tools are never run during an Evaluation; tools without a result here get a neutral stub.</p>
        {draft.cannedToolResults.map((result, i) => (
          <div className={styles.item} key={i}>
            <ToolSelect
              id={`${idPrefix}-canned-${i}`}
              value={result.toolName}
              tools={tools}
              disabled={readOnly}
              onChange={(toolName) => set({ cannedToolResults: replaceAt(draft.cannedToolResults, i, { ...result, toolName }) })}
            />
            <Textarea
              aria-label={`Canned result ${i + 1}`}
              mono
              rows={2}
              value={result.result}
              disabled={readOnly}
              onChange={(e) =>
                set({ cannedToolResults: replaceAt(draft.cannedToolResults, i, { ...result, result: e.target.value }) })
              }
            />
            {!readOnly && (
              <Button
                iconOnly
                variant="ghost"
                aria-label={`Remove canned result ${i + 1}`}
                icon={<Trash2 size={14} />}
                onClick={() => set({ cannedToolResults: removeAt(draft.cannedToolResults, i) })}
              />
            )}
          </div>
        ))}
      </section>

      <section className={styles.group} aria-label="Checks">
        <p className={styles.groupTitle}>Checks</p>
        <div className={styles.checkGrid}>
          {CHECKS.map((info) => {
            const selected = draft.checks.find((c) => c.type === info.type);
            return (
              <label className={styles.checkOption} key={info.type}>
                <input
                  type="checkbox"
                  checked={selected !== undefined}
                  disabled={readOnly}
                  onChange={(e) => toggleCheck(info.type, e.target.checked)}
                />
                {info.label}
                {info.quality && selected && (
                  <Input
                    className={styles.threshold}
                    controlSize="sm"
                    inputMode="numeric"
                    aria-label={`${info.label} threshold`}
                    placeholder={String(DEFAULT_QUALITY_THRESHOLD)}
                    value={selected.threshold}
                    disabled={readOnly}
                    onChange={(e) =>
                      set({
                        checks: draft.checks.map((c) => (c.type === info.type ? { ...c, threshold: e.target.value } : c)),
                      })
                    }
                  />
                )}
              </label>
            );
          })}
        </div>
        <p className={styles.muted}>Quality Checks are scored 1-5 by the Judge and pass at the threshold (default 4).</p>
      </section>
    </div>
  );
}
