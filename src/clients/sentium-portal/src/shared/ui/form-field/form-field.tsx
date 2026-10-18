import styles from "./form-field.module.scss";

interface FormFieldProps {
  id?: string;
  label: React.ReactNode;
  charCount?: { current: number; max: number };
  children: React.ReactNode;
}

const FormField = ({ id, label, charCount, children }: FormFieldProps) => {
  return (
    <div className={styles.group}>
      <div className={styles.labelRow}>
        <label className={styles.label} htmlFor={id}>
          {label}
        </label>
        {charCount !== undefined && (
          <span className={styles.charCount}>
            {charCount.current}/{charCount.max}
          </span>
        )}
      </div>
      {children}
    </div>
  );
};

export default FormField;
