import styles from "./page-header.module.scss";

interface PageHeaderProps {
  icon?: React.ReactNode;
  title: string;
  subtitle?: string;
  right?: React.ReactNode;
  className?: string;
}

const PageHeader = ({ icon, title, subtitle, right, className }: PageHeaderProps) => {
  return (
    <div className={`${styles.header} ${className ?? ""}`}>
      <div className={styles.headerLeft}>
        {icon && <div className={styles.iconWrap}>{icon}</div>}
        <div className={styles.text}>
          <h1 className={styles.title}>{title}</h1>
          {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
        </div>
      </div>
      {right && <div className={styles.right}>{right}</div>}
    </div>
  );
};

export default PageHeader;
