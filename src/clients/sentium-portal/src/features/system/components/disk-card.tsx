import { Database } from "lucide-react";
import styles from "../system.module.css";
import type { DiskInfo } from "@/features/system/types/system";
import { formatGb, usageTone } from "../system-utils";
import { ProgressGroup } from "./progress-group";

export function DiskCard({ disk }: { disk: DiskInfo }) {
  return (
    <div className={styles.disk}>
      <div className={styles.diskHeader}>
        <span className={styles.diskName}>
          <Database size={14} aria-hidden="true" />
          {disk.name}
        </span>
        <span className={styles.diskLabel}>{disk.label || disk.fileSystem}</span>
      </div>
      <ProgressGroup
        name={`${formatGb(disk.usedGb)} used`}
        value={`${disk.usagePercent.toFixed(1)}%`}
        percent={disk.usagePercent}
        tone={usageTone(disk.usagePercent)}
      />
      <span className={styles.diskMeta}>
        {formatGb(disk.availableGb)} free of {formatGb(disk.totalGb)}
      </span>
    </div>
  );
}
