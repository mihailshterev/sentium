import { Clock } from "lucide-react";
import type { DeviceStatus } from "@/features/devices";
import { usageTone } from "@/features/system/system-utils";
import { formatRelativeTime } from "@/shared/utils/formatters";
import styles from "../vigil.module.css";

const BYTES_PER_GB = 1024 ** 3;

const usedPercent = (total: number, available: number) => (total > 0 ? ((total - available) / total) * 100 : 0);

function Usage({ percent, label }: { percent: number; label?: string }) {
  return (
    <div className={styles.latencyCell}>
      <span className={styles.latencyValue}>
        {label && `${label} `}
        {percent.toFixed(0)}%
      </span>
      <div className={styles.latencyTrack}>
        <div className={styles.latencyFill} data-tone={usageTone(percent)} style={{ width: `${Math.min(percent, 100)}%` }} />
      </div>
    </div>
  );
}

/** One paired PC. Offline is a normal state (asleep or switched off), so it is shown muted, not as a fault. */
export function DeviceRow({ device, showOwner }: { device: DeviceStatus; showOwner: boolean }) {
  const report = device.report;

  return (
    <div className={styles.deviceRow} data-online={device.isOnline || undefined} data-testid="device-row">
      <div className={styles.statusCell}>
        <span className={styles.deviceDot} aria-hidden="true" />
        <span className={styles.deviceState}>{device.isOnline ? "Online" : "Offline"}</span>
      </div>

      <div className={styles.nameCell}>
        <span className={styles.serviceName}>{device.name}</span>
        <span className={styles.uptime}>
          {showOwner && device.ownerUserName ? `${device.ownerUserName} · ` : ""}
          {report?.osDescription ?? "No report yet"}
        </span>
      </div>

      {report ? (
        <>
          <Usage percent={report.cpuPercent} />
          <Usage percent={usedPercent(report.memoryTotalBytes, report.memoryAvailableBytes)} />
          <div className={styles.deviceDisks}>
            {report.disks.map((disk) => (
              <Usage
                key={disk.name}
                label={disk.name}
                percent={usedPercent(disk.totalBytes, disk.availableBytes)}
              />
            ))}
          </div>
        </>
      ) : (
        <span className={styles.deviceNoReport}>—</span>
      )}

      <div className={styles.checkedCell} title={report ? `${(report.memoryTotalBytes / BYTES_PER_GB).toFixed(0)} GB RAM` : undefined}>
        <Clock size={13} aria-hidden="true" />
        <span>{device.lastSeenAt ? formatRelativeTime(device.lastSeenAt) : "never"}</span>
      </div>
    </div>
  );
}
