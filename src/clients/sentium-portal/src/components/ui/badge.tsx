import styles from "./badge.module.scss";

export type BadgeTone = "green" | "blue" | "purple" | "amber" | "red" | "cyan" | "neutral";

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
  dot?: boolean;
}

const Badge = ({ tone = "neutral", dot = false, className, children, ...rest }: BadgeProps) => {
  return (
    <span className={`${styles.badge} ${styles[tone]} ${className ?? ""}`} {...rest}>
      {dot && <span className={styles.dot} />}
      {children}
    </span>
  );
};

export default Badge;
