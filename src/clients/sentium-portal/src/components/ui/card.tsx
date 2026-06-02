import styles from "./card.module.scss";

interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  padded?: boolean;
  interactive?: boolean;
}

const Card = ({ padded = false, interactive = false, className, children, ...rest }: CardProps) => {
  const classes = [styles.card, padded ? styles.padded : "", interactive ? styles.interactive : "", className ?? ""]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
};

export default Card;
