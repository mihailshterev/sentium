import styles from "./page-header.module.css";

interface PageHeaderProps {
  icon?: React.ReactNode;
  /** Interactive element before the title, e.g. a back button. */
  leading?: React.ReactNode;
  title: string;
  subtitle?: React.ReactNode;
  right?: React.ReactNode;
  /** Set by `Page` once its body scrolls: the header condenses into a framed bar. */
  scrolled?: boolean;
  /** Matches the page body's max-width so the title lines up with the content. */
  width?: "default" | "wide" | "full";
  className?: string;
}

export function PageHeader({
  icon,
  leading,
  title,
  subtitle,
  right,
  scrolled = false,
  width = "default",
  className,
}: PageHeaderProps) {
  return (
    <header
      className={[styles.header, className ?? ""].filter(Boolean).join(" ")}
      data-scrolled={scrolled || undefined}
      data-width={width}
    >
      <div className={styles.inner}>
        <div className={styles.mainRow}>
          <div className={styles.headerLeft}>
            {leading}
            {icon && (
              <div className={styles.iconWrap} aria-hidden="true">
                {icon}
              </div>
            )}
            <div className={styles.text}>
              <h1 className={styles.title}>{title}</h1>
              {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
            </div>
          </div>
          {right && <div className={styles.right}>{right}</div>}
        </div>
      </div>
    </header>
  );
}
