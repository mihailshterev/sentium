import { Loader2 } from "lucide-react";
import styles from "./button.module.scss";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
type ButtonSize = "sm" | "md";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  icon?: React.ReactNode;
  fullWidth?: boolean;
}

const Button = ({
  variant = "secondary",
  size = "md",
  loading = false,
  icon,
  fullWidth = false,
  className,
  children,
  disabled,
  ...rest
}: ButtonProps) => {
  const classes = [styles.btn, styles[variant], styles[size], fullWidth ? styles.fullWidth : "", className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <button className={classes} disabled={disabled || loading} {...rest}>
      {loading ? <Loader2 size={size === "sm" ? 13 : 15} className={styles.spinner} /> : icon}
      {children != null && <span className={styles.label}>{children}</span>}
    </button>
  );
};

export default Button;
