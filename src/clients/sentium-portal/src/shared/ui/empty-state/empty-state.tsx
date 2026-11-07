import styles from "./empty-state.module.css";

interface EmptyStateProps {
  icon: React.ReactNode;
  title: string;
  hint?: React.ReactNode;
  action?: React.ReactNode;
  /** Tighter variant for small panels. */
  compact?: boolean;
}

export function EmptyState({ icon, title, hint, action, compact = false }: EmptyStateProps) {
  return (
    <div className={`${styles.wrap} ${compact ? styles.compact : ""}`}>
      <div className={styles.icon} aria-hidden="true">
        {icon}
      </div>
      <p className={styles.title}>{title}</p>
      {hint && <p className={styles.hint}>{hint}</p>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}
