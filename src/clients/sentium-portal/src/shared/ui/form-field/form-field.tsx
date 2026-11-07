import styles from "./form-field.module.css";

interface FormFieldProps {
  id?: string;
  label: React.ReactNode;
  /** Helper text under the control. */
  hint?: React.ReactNode;
  /** Validation message; replaces the hint and is announced. */
  error?: React.ReactNode;
  charCount?: { current: number; max: number };
  className?: string;
  children: React.ReactNode;
}

export function FormField({ id, label, hint, error, charCount, className, children }: FormFieldProps) {
  const over = charCount !== undefined && charCount.current > charCount.max;
  return (
    <div className={[styles.group, className ?? ""].filter(Boolean).join(" ")}>
      <div className={styles.labelRow}>
        <label className={styles.label} htmlFor={id}>
          {label}
        </label>
        {charCount !== undefined && (
          <span className={`${styles.charCount} ${over ? styles.over : ""}`}>
            {charCount.current}/{charCount.max}
          </span>
        )}
      </div>
      {children}
      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : (
        hint && <p className={styles.hint}>{hint}</p>
      )}
    </div>
  );
}
