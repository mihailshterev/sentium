import { Loader2 } from "lucide-react";
import { buttonClass, type ButtonSize, type ButtonStyleOptions } from "./button-class";
import styles from "./button.module.css";

const ICON_SIZE: Record<ButtonSize, number> = { sm: 14, md: 16, lg: 18 };

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement>, Omit<ButtonStyleOptions, "className"> {
  ref?: React.Ref<HTMLButtonElement>;
  loading?: boolean;
  icon?: React.ReactNode;
  /** Icon rendered after the label (e.g. an arrow). */
  iconRight?: React.ReactNode;
}

export function Button({
  variant = "secondary",
  size = "md",
  iconOnly = false,
  fullWidth = false,
  loading = false,
  icon,
  iconRight,
  className,
  children,
  disabled,
  type = "button",
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClass({ variant, size, iconOnly, fullWidth, className })}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? <Loader2 size={ICON_SIZE[size]} className={styles.spinner} aria-hidden="true" /> : icon}
      {children != null && <span className={styles.label}>{children}</span>}
      {!loading && iconRight}
    </button>
  );
}
