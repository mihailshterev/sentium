import styles from "./stat-card.module.scss";

type StatCardColor = "green" | "blue" | "amber" | "purple" | "red" | "cyan";
type StatCardChipVariant = "green" | "red" | "amber";

interface StatCardProps {
  icon: React.ReactNode;
  value: React.ReactNode;
  label: string;
  iconColor?: StatCardColor;
  chip?: string;
  chipVariant?: StatCardChipVariant;
}

const COLOR_MAP: Record<StatCardColor, string> = {
  green: styles.iconGreen,
  blue: styles.iconBlue,
  amber: styles.iconAmber,
  purple: styles.iconPurple,
  red: styles.iconRed,
  cyan: styles.iconCyan,
};

const CHIP_MAP: Record<StatCardChipVariant, string> = {
  green: styles.chipGreen,
  red: styles.chipRed,
  amber: styles.chipAmber,
};

const StatCard = ({ icon, value, label, iconColor = "green", chip, chipVariant = "green" }: StatCardProps) => {
  return (
    <div className={styles.card}>
      <div className={`${styles.icon} ${COLOR_MAP[iconColor]}`}>{icon}</div>
      <div className={styles.content}>
        <span className={styles.value}>{value}</span>
        <span className={styles.label}>{label}</span>
      </div>
      {chip && <span className={`${styles.chip} ${CHIP_MAP[chipVariant]}`}>{chip}</span>}
    </div>
  );
};

export default StatCard;
