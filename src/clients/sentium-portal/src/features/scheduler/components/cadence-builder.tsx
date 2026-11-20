import { useId } from "react";
import styles from "../scheduler.module.css";
import { Input, Select } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { type Cadence, type CadenceType, WEEKDAY_LABELS, describeCadence, toCron, withType } from "../utils/cadence";

const CADENCE_OPTIONS: { value: CadenceType; label: string }[] = [
  { value: "daily", label: "Every day" },
  { value: "weekdays", label: "Weekdays (Mon–Fri)" },
  { value: "weekly", label: "Weekly, on chosen days" },
  { value: "monthly", label: "Monthly" },
  { value: "hourly", label: "Every hour" },
  { value: "custom", label: "Custom cron expression" },
];

/** Monday first, the way a week reads. */
const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

interface CadenceBuilderProps {
  value: Cadence;
  onChange: (cadence: Cadence) => void;
  /** Shown under the preview, for example the time zone the times are read in. */
  zoneHint: string;
}

export function CadenceBuilder({ value, onChange, zoneHint }: CadenceBuilderProps) {
  const id = useId();
  const cron = toCron(value);

  const toggleDay = (day: number, on: boolean) => {
    if (value.type !== "weekly") {
      return;
    }
    const days = on ? [...value.days, day] : value.days.filter((d) => d !== day);
    onChange({ ...value, days: days.length > 0 ? days : value.days });
  };

  return (
    <div className={styles.cadence}>
      <div className={styles.cadenceRow}>
        <FormField id={`${id}-type`} label="Repeat" className={styles.cadenceType}>
          <Select
            id={`${id}-type`}
            value={value.type}
            onChange={(e) => onChange(withType(value, e.target.value as CadenceType))}
          >
            {CADENCE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </FormField>

        {"time" in value && (
          <FormField id={`${id}-time`} label="At">
            <Input
              id={`${id}-time`}
              type="time"
              required
              value={value.time}
              onChange={(e) => onChange({ ...value, time: e.target.value })}
            />
          </FormField>
        )}

        {value.type === "hourly" && (
          <FormField id={`${id}-minute`} label="Minute past the hour">
            <Input
              id={`${id}-minute`}
              type="number"
              min={0}
              max={59}
              value={value.minute}
              onChange={(e) => onChange({ ...value, minute: clamp(Number(e.target.value), 0, 59) })}
            />
          </FormField>
        )}

        {value.type === "monthly" && (
          <FormField id={`${id}-day`} label="Day of month" hint="1–28 runs every month.">
            <Input
              id={`${id}-day`}
              type="number"
              min={1}
              max={31}
              value={value.day}
              onChange={(e) => onChange({ ...value, day: clamp(Number(e.target.value), 1, 31) })}
            />
          </FormField>
        )}
      </div>

      {value.type === "weekly" && (
        <div className={styles.dayPicker} role="group" aria-label="Days of the week">
          {WEEK_ORDER.map((day) => (
            <label key={day} className={styles.dayOption}>
              <input
                type="checkbox"
                checked={value.days.includes(day)}
                onChange={(e) => toggleDay(day, e.target.checked)}
              />
              {WEEKDAY_LABELS[day].slice(0, 3)}
            </label>
          ))}
        </div>
      )}

      {value.type === "custom" && (
        <FormField
          id={`${id}-cron`}
          label="Cron expression"
          hint="Quartz syntax with seconds first, for example 0 30 7 ? * MON-FRI. Runs must be at least a minute apart."
        >
          <Input
            id={`${id}-cron`}
            className={styles.monoInput}
            value={value.cron}
            spellCheck={false}
            onChange={(e) => onChange({ type: "custom", cron: e.target.value })}
          />
        </FormField>
      )}

      <p className={styles.cadencePreview} data-testid="cadence-preview">
        {describeCadence(cron)} <code className={styles.cron}>{cron}</code>
        <span className={styles.zoneHint}>{zoneHint}</span>
      </p>
    </div>
  );
}

function clamp(n: number, min: number, max: number) {
  return Number.isFinite(n) ? Math.min(max, Math.max(min, Math.round(n))) : min;
}
