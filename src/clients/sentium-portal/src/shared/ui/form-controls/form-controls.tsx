import { useId } from "react";
import { ChevronDown, Search } from "lucide-react";
import styles from "./form-controls.module.css";

const cx = (...classes: (string | false | null | undefined)[]) => classes.filter(Boolean).join(" ");

type ControlSize = "sm" | "md";

interface ControlProps {
  /** Shows the error state and sets aria-invalid. */
  invalid?: boolean;
  controlSize?: ControlSize;
}

type InputProps = React.InputHTMLAttributes<HTMLInputElement> & ControlProps & { ref?: React.Ref<HTMLInputElement> };

export function Input({ invalid, controlSize = "md", className, ...rest }: InputProps) {
  return (
    <input
      className={cx(styles.control, styles[controlSize], invalid && styles.invalid, className)}
      aria-invalid={invalid || undefined}
      {...rest}
    />
  );
}

type TextareaProps = React.TextareaHTMLAttributes<HTMLTextAreaElement> &
  Omit<ControlProps, "controlSize"> & { ref?: React.Ref<HTMLTextAreaElement>; mono?: boolean };

export function Textarea({ invalid, mono = false, className, ...rest }: TextareaProps) {
  return (
    <textarea
      className={cx(styles.control, styles.textarea, mono && styles.mono, invalid && styles.invalid, className)}
      aria-invalid={invalid || undefined}
      {...rest}
    />
  );
}

type SelectProps = React.SelectHTMLAttributes<HTMLSelectElement> &
  ControlProps & { ref?: React.Ref<HTMLSelectElement>; wrapperClassName?: string };

/** Native select, styled. Keeps native keyboard and mobile behaviour. */
export function Select({ invalid, controlSize = "md", className, wrapperClassName, children, ...rest }: SelectProps) {
  return (
    <span className={cx(styles.selectWrap, wrapperClassName)}>
      <select
        className={cx(styles.control, styles[controlSize], styles.select, invalid && styles.invalid, className)}
        aria-invalid={invalid || undefined}
        {...rest}
      >
        {children}
      </select>
      <ChevronDown size={15} className={styles.chevron} aria-hidden="true" />
    </span>
  );
}

type SearchInputProps = Omit<InputProps, "type"> & { wrapperClassName?: string };

export function SearchInput({ controlSize = "md", className, wrapperClassName, ...rest }: SearchInputProps) {
  return (
    <span className={cx(styles.searchWrap, wrapperClassName)}>
      <Search size={15} className={styles.searchIcon} aria-hidden="true" />
      <input type="search" className={cx(styles.control, styles[controlSize], styles.search, className)} {...rest} />
    </span>
  );
}

type RangeProps = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> & {
  ref?: React.Ref<HTMLInputElement>;
  /** Filled portion (0-100). Defaults to the position of `value`; pass it for uncontrolled inputs. */
  fillPercent?: number;
};

/** Native range slider with a filled track. */
export function Range({ className, style, fillPercent, min = 0, max = 100, value, ...rest }: RangeProps) {
  const computed =
    value !== undefined ? ((Number(value) - Number(min)) / (Number(max) - Number(min) || 1)) * 100 : undefined;
  const fill = Math.min(100, Math.max(0, fillPercent ?? computed ?? 0));
  return (
    <input
      type="range"
      className={cx(styles.range, className)}
      style={{ "--fill": `${fill}%`, ...style } as React.CSSProperties}
      min={min}
      max={max}
      value={value}
      {...rest}
    />
  );
}

type SwitchProps = Omit<React.InputHTMLAttributes<HTMLInputElement>, "type" | "role"> & {
  ref?: React.Ref<HTMLInputElement>;
};

/** Checkbox styled as a switch; remains an `input[type=checkbox]` for forms and tests. */
export function Switch({ className, ...rest }: SwitchProps) {
  return (
    <span className={cx(styles.switch, className)}>
      <input type="checkbox" role="switch" className={styles.switchInput} {...rest} />
      <span className={styles.switchTrack} aria-hidden="true">
        <span className={styles.switchThumb} />
      </span>
    </span>
  );
}

interface SwitchRowProps extends SwitchProps {
  label: React.ReactNode;
  description?: React.ReactNode;
}

/**
 * Label + description on the left, switch on the right; the whole row is clickable.
 * Structure (label text -> info block -> row containing the checkbox) is relied on by e2e page objects.
 */
export function SwitchRow({ label, description, className, ...switchProps }: SwitchRowProps) {
  const id = useId();
  return (
    <label className={cx(styles.switchRow, className)}>
      <span className={styles.switchInfo}>
        <span id={`${id}-label`} className={styles.switchLabel}>
          {label}
        </span>
        {description && (
          <span id={`${id}-desc`} className={styles.switchDesc}>
            {description}
          </span>
        )}
      </span>
      <Switch
        aria-labelledby={`${id}-label`}
        aria-describedby={description ? `${id}-desc` : undefined}
        {...switchProps}
      />
    </label>
  );
}
