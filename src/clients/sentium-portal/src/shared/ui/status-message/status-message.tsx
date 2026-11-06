import styles from "./status-message.module.css";

type StatusMessageVariant = "success" | "error" | "loading" | "info" | "warning";

interface StatusMessageProps {
  variant: StatusMessageVariant;
  icon?: React.ReactNode;
  message: React.ReactNode;
  className?: string;
}

/** Inline feedback banner. Errors are announced assertively, everything else politely. */
export function StatusMessage({ variant, icon, message, className }: StatusMessageProps) {
  return (
    <div
      className={[styles.msg, styles[variant], className ?? ""].filter(Boolean).join(" ")}
      role={variant === "error" ? "alert" : "status"}
    >
      {icon && (
        <span className={styles.icon} aria-hidden="true">
          {icon}
        </span>
      )}
      <span className={styles.text}>{message}</span>
    </div>
  );
}
