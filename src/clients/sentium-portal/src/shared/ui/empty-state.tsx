import styles from "./empty-state.module.scss";

interface EmptyStateProps {
  icon: React.ReactNode;
  title: string;
  hint?: string;
  action?: React.ReactNode;
}

const EmptyState = ({ icon, title, hint, action }: EmptyStateProps) => {
  return (
    <div className={styles.wrap}>
      <div className={styles.icon}>{icon}</div>
      <span className={styles.title}>{title}</span>
      {hint && <span className={styles.hint}>{hint}</span>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
};

export default EmptyState;
