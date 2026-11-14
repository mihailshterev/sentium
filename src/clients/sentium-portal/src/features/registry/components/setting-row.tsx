import { RotateCcw } from "lucide-react";
import { Button } from "@/shared/ui/button";
import type { SettingDefinition, SettingValue } from "../types/settings";
import { SettingControl } from "./setting-control";
import styles from "../settings.module.css";

/** Controls that read best under their text rather than beside it. */
const STACKED = new Set(["textarea", "range"]);

interface SettingRowProps {
  definition: SettingDefinition;
  value: SettingValue;
  isDirty: boolean;
  error: string | undefined;
  disabled: boolean;
  inheritZoneLabel: string;
  onChange: (value: SettingValue) => void;
}

export function SettingRow({
  definition,
  value,
  isDirty,
  error,
  disabled,
  inheritZoneLabel,
  onChange,
}: SettingRowProps) {
  const inputId = `setting-${definition.id}`;
  const descriptionId = `${inputId}-desc`;
  const errorId = `${inputId}-error`;
  const isModified = value !== definition.defaultValue;

  return (
    <div
      className={styles.row}
      data-layout={STACKED.has(definition.control.kind) ? "stacked" : "inline"}
      data-modified={isModified || undefined}
      data-dirty={isDirty || undefined}
    >
      <div className={styles.rowText}>
        <div className={styles.rowHead}>
          <label htmlFor={inputId} className={styles.rowTitle}>
            {definition.title}
          </label>
          {isDirty ? (
            <span className={styles.rowFlag}>unsaved</span>
          ) : (
            isModified && <span className={styles.rowFlag}>modified</span>
          )}
          {isModified && !disabled && (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              className={styles.resetButton}
              icon={<RotateCcw size={13} />}
              aria-label={`Reset ${definition.title} to default`}
              title="Reset to default"
              onClick={() => onChange(definition.defaultValue)}
            />
          )}
        </div>
        <p id={descriptionId} className={styles.rowDescription}>
          {definition.description}
        </p>
      </div>

      <div className={styles.rowControl}>
        <SettingControl
          definition={definition}
          inputId={inputId}
          describedBy={error ? `${descriptionId} ${errorId}` : descriptionId}
          value={value}
          onChange={onChange}
          invalid={Boolean(error)}
          disabled={disabled}
          inheritZoneLabel={inheritZoneLabel}
        />
        {error && (
          <p id={errorId} className={styles.rowError}>
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
