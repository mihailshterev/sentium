import { useModels } from "@/features/models/hooks/useModels";
import { Input, Range, Select, Switch, Textarea } from "@/shared/ui/form-controls";
import type { SettingDefinition, SettingValue } from "../types/settings";
import styles from "../settings.module.css";

const TIME_ZONES = [...new Set(["UTC", ...Intl.supportedValuesOf("timeZone")])];

export interface SettingControlProps {
  definition: SettingDefinition;
  inputId: string;
  describedBy: string;
  value: SettingValue;
  onChange: (value: SettingValue) => void;
  invalid: boolean;
  disabled: boolean;
  /** Label of the empty "inherit" time zone option, e.g. "Household default (UTC)". */
  inheritZoneLabel: string;
}

export function SettingControl({
  definition,
  inputId,
  describedBy,
  value,
  onChange,
  invalid,
  disabled,
  inheritZoneLabel,
}: SettingControlProps) {
  const { control, testId } = definition;
  const common = { id: inputId, "aria-describedby": describedBy, disabled, "data-testid": testId };

  switch (control.kind) {
    case "switch":
      return <Switch {...common} checked={Boolean(value)} onChange={(e) => onChange(e.target.checked)} />;

    case "number":
      return (
        <span className={styles.numberControl}>
          <Input
            {...common}
            type="number"
            inputMode="numeric"
            min={control.min}
            max={control.max}
            invalid={invalid}
            value={Number.isNaN(value) ? "" : String(value)}
            onChange={(e) => onChange(e.target.value === "" ? "" : e.target.valueAsNumber)}
          />
          <span className={styles.unit}>{control.unit}</span>
        </span>
      );

    case "range": {
      const number = Number(value);
      return (
        <div className={styles.rangeControl}>
          <div className={styles.rangeTrack}>
            <Range
              {...common}
              min={control.min}
              max={control.max}
              step={control.step}
              value={number}
              onChange={(e) => onChange(Number(e.target.value))}
            />
            <output htmlFor={inputId} className={styles.rangeValue}>
              {control.format(number)}
            </output>
          </div>
          <div className={styles.rangeLabels}>
            <span>{control.minLabel}</span>
            <span>{control.maxLabel}</span>
          </div>
          {control.explain && <p className={styles.rangeExplain}>{control.explain(number)}</p>}
        </div>
      );
    }

    case "textarea": {
      const length = String(value).length;
      return (
        <div className={styles.textareaControl}>
          <Textarea
            {...common}
            mono
            rows={control.rows}
            spellCheck={false}
            invalid={invalid}
            placeholder={control.placeholder}
            value={String(value)}
            onChange={(e) => onChange(e.target.value)}
          />
          <span className={styles.charCount} data-over={length > control.maxLength || undefined}>
            {length.toLocaleString()} / {control.maxLength.toLocaleString()}
          </span>
        </div>
      );
    }

    case "time":
      return (
        <Input
          {...common}
          type="time"
          className={styles.compactControl}
          required={control.required}
          invalid={invalid}
          value={String(value)}
          onChange={(e) => onChange(e.target.value)}
        />
      );

    case "timezone":
      return (
        <Select
          {...common}
          wrapperClassName={styles.compactControl}
          value={String(value)}
          onChange={(e) => onChange(e.target.value)}
        >
          {control.inherit && <option value="">{inheritZoneLabel}</option>}
          {TIME_ZONES.map((zone) => (
            <option key={zone} value={zone}>
              {zone}
            </option>
          ))}
        </Select>
      );

    case "model":
      return <ModelControl {...common} value={String(value)} onChange={onChange} emptyLabel={control.emptyLabel} />;
  }
}

interface ModelControlProps {
  id: string;
  "aria-describedby": string;
  disabled: boolean;
  value: string;
  onChange: (value: string) => void;
  emptyLabel: string | undefined;
}

/**
 * Lists the served models plus the stored one (it may no longer be served), so the select never silently shows a
 * different model than the saved value. Free text when the inference server lists nothing.
 */
function ModelControl({ value, onChange, emptyLabel, ...rest }: ModelControlProps) {
  const { models } = useModels();

  if (models.length === 0) {
    return (
      <Input
        {...rest}
        className={styles.compactControl}
        type="text"
        autoComplete="off"
        spellCheck={false}
        placeholder={emptyLabel ?? "e.g. gemma-4-e4b"}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    );
  }

  const options = [...new Set(value ? [value, ...models] : models)].sort((a, b) => a.localeCompare(b));
  return (
    <Select {...rest} wrapperClassName={styles.compactControl} value={value} onChange={(e) => onChange(e.target.value)}>
      {emptyLabel !== undefined ? (
        <option value="">{emptyLabel}</option>
      ) : (
        !value && (
          <option value="" disabled>
            Select a model
          </option>
        )
      )}
      {options.map((model) => (
        <option key={model} value={model}>
          {model}
        </option>
      ))}
    </Select>
  );
}
