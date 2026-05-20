import { Database } from "lucide-react";
import styles from "../system.module.scss";
import type { DiskInfo } from "../../../types/system";
import { formatGb } from "../system-utils";

function getUsageColor(percent: number): string {
  if (percent < 50) {
    return styles.fillGreen;
  }

  if (percent < 75) {
    return styles.fillAmber;
  }

  return styles.fillRed;
}

const DiskCard = ({ disk }: { disk: DiskInfo }) => (
  <div className={styles.diskCard}>
    <div className={styles.diskHeader}>
      <span className={styles.diskName}>
        <Database size={13} />
        {disk.name}
      </span>
      <span className={styles.diskLabel}>{disk.label || disk.fileSystem}</span>
    </div>
    <div className={styles.progressGroup}>
      <div className={styles.progressLabel}>
        <span className={styles.progressName}>{formatGb(disk.usedGb)} used</span>
        <span className={styles.progressValue}>{disk.usagePercent.toFixed(1)}%</span>
      </div>
      <div className={styles.progressTrack}>
        <div
          className={`${styles.progressFill} ${getUsageColor(disk.usagePercent)}`}
          style={{ width: `${Math.min(disk.usagePercent, 100)}%` }}
        />
      </div>
    </div>
    <span className={styles.diskMeta}>
      {formatGb(disk.availableGb)} free of {formatGb(disk.totalGb)}
    </span>
  </div>
);

export default DiskCard;
