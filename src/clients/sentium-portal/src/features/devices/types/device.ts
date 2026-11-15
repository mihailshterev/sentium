/** A paired PC as Identity knows it. */
export interface Device {
  id: string;
  name: string;
  ownerUserId: string;
  ownerUserName: string | null;
  appVersion: string | null;
  createdAt: string;
  lastSeenAt: string | null;
}

export interface PairingCode {
  code: string;
  expiresAt: string;
}

export interface DeviceDisk {
  name: string;
  totalBytes: number;
  availableBytes: number;
}

/** One health snapshot sent by the Satelles app. */
export interface DeviceReport {
  machineName: string;
  osDescription: string;
  uptimeSeconds: number;
  appVersion: string;
  cpuPercent: number;
  memoryTotalBytes: number;
  memoryAvailableBytes: number;
  disks: DeviceDisk[];
}

/** A Device with its live state from Vigil; `report` is null until one arrives after a server restart. */
export interface DeviceStatus {
  id: string;
  name: string;
  ownerUserId: string;
  ownerUserName: string | null;
  appVersion: string | null;
  isOnline: boolean;
  lastSeenAt: string | null;
  report: DeviceReport | null;
}

/** `device.updated` frame on the Vigil stream. */
export interface DeviceUpdate {
  id: string;
  isOnline: boolean;
  lastSeenAt: string;
  report: DeviceReport;
}
