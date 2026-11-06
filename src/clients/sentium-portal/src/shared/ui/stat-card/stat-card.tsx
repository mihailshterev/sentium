import styles from "./stat-card.module.css";

type StatCardColor = "green" | "blue" | "amber" | "purple" | "red" | "cyan" | "accent" | "neutral";
type StatCardChipVariant = "green" | "red" | "amber" | "neutral";

interface StatCardProps {
  icon: React.ReactNode;
  value: React.ReactNode;
  label: string;
  /** Tints the icon and the value's leading marker. */
  iconColor?: StatCardColor;
  chip?: string;
  chipVariant?: StatCardChipVariant;
  /** Small text under the value, e.g. "+12 this week". */
  hint?: React.ReactNode;
}

/** A single data cell: mono label, large value. Group cells with `StatGrid` for a spec-sheet table. */
export function StatCard({
  icon,
  value,
  label,
  iconColor = "neutral",
  chip,
  chipVariant = "neutral",
  hint,
}: StatCardProps) {
  return (
    <div className={`frame ${styles.cell} ${styles[iconColor]}`}>
      <div className={styles.top}>
        <span className={styles.label}>{label}</span>
        <span className={styles.icon} aria-hidden="true">
          {icon}
        </span>
      </div>
      <div className={styles.bottom}>
        <span className={styles.value}>{value}</span>
        {chip && <span className={`${styles.chip} ${styles[`chip_${chipVariant}`]}`}>{chip}</span>}
      </div>
      {hint && <span className={styles.hint}>{hint}</span>}
    </div>
  );
}

interface StatGridProps {
  /** Minimum cell width before wrapping. */
  minCellWidth?: number;
  /** Fixed column count instead of auto-fit wrapping (avoids orphan cells, e.g. 3 × 2). */
  columns?: number;
  className?: string;
  children: React.ReactNode;
  "aria-busy"?: boolean;
}

/** Framed panel that joins StatCards into one hairline-divided grid. */
export function StatGrid({ minCellWidth = 170, columns, className, children, ...rest }: StatGridProps) {
  return (
    <div
      className={["frame", styles.grid, className ?? ""].filter(Boolean).join(" ")}
      style={
        {
          "--min-cell": `${minCellWidth}px`,
          ...(columns ? { "--cols": `repeat(${columns}, minmax(0, 1fr))` } : {}),
        } as React.CSSProperties
      }
      {...rest}
    >
      {children}
    </div>
  );
}
