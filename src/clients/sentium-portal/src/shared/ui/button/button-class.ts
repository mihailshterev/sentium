import styles from "./button.module.css";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "sm" | "md" | "lg";

export interface ButtonStyleOptions {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Square button holding only an icon. Give it an `aria-label`. */
  iconOnly?: boolean;
  fullWidth?: boolean;
  className?: string;
}

/** Class names for anything that should look like a button, e.g. a router `<Link>`. */
export function buttonClass({
  variant = "secondary",
  size = "md",
  iconOnly = false,
  fullWidth = false,
  className,
}: ButtonStyleOptions = {}): string {
  return [
    styles.btn,
    styles[variant],
    styles[size],
    iconOnly ? styles.iconOnly : "",
    fullWidth ? styles.fullWidth : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");
}
