import styles from "./card.module.css";

type CardPadding = "none" | "sm" | "md" | "lg";

interface CardProps extends React.HTMLAttributes<HTMLElement> {
  /** Shorthand for `padding="md"`. */
  padded?: boolean;
  padding?: CardPadding;
  /** Hover lift for clickable cards. */
  interactive?: boolean;
  /** Corner registration ticks on the frame (default true). */
  corners?: boolean;
  as?: "div" | "section" | "article" | "li";
}

const PADDING_CLASS: Record<CardPadding, string> = {
  none: "",
  sm: styles.padSm,
  md: styles.padded,
  lg: styles.padLg,
};

/** Framed panel over the field: hairline border plus corner registration ticks. */
export function Card({
  padded = false,
  padding,
  interactive = false,
  corners = true,
  as: Tag = "div",
  className,
  children,
  ...rest
}: CardProps) {
  const pad = padding ?? (padded ? "md" : "none");
  const classes = [
    styles.card,
    corners ? "frame" : "",
    PADDING_CLASS[pad],
    interactive ? styles.interactive : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <Tag className={classes} data-padding={pad} {...rest}>
      {children}
    </Tag>
  );
}

interface CardHeaderProps {
  title: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  /** Right-aligned controls (buttons, badges, links). */
  actions?: React.ReactNode;
  className?: string;
}

export function CardHeader({ title, description, icon, actions, className }: CardHeaderProps) {
  return (
    <div className={[styles.header, className ?? ""].filter(Boolean).join(" ")}>
      <div className={styles.headerMain}>
        {icon && (
          <span className={styles.headerIcon} aria-hidden="true">
            {icon}
          </span>
        )}
        <div className={styles.headerText}>
          <h2 className={styles.title}>{title}</h2>
          {description && <p className={styles.description}>{description}</p>}
        </div>
      </div>
      {actions && <div className={styles.actions}>{actions}</div>}
    </div>
  );
}
