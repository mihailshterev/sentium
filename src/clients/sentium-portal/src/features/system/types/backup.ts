export type BackupHealth = "Unknown" | "Healthy" | "Degraded" | "Unhealthy";

export interface BackupArchive {
  name: string;
  createdAt: string;
  sizeBytes: number;
  encrypted: boolean;
}

export interface BackupRun {
  startedAt: string;
  finishedAt: string;
  trigger: "scheduled" | "manual";
  succeeded: boolean;
  archive: string | null;
  error: string | null;
}

export interface BackupStatus {
  sentiumVersion: string;
  serviceStartedAt: string;
  destinationReady: boolean;
  destinationProblem: string | null;
  running: boolean;
  nextRunAt: string | null;
  lastRun: BackupRun | null;
  /** Newest first; every entry is a verified backup. */
  backups: BackupArchive[];
  keepDaily: number;
  keepWeekly: number;
  encryptionEnabled: boolean;
}

/** `status` is null when backups are not set up or the backup service is unreachable. */
export interface BackupsOverview {
  configured: boolean;
  maxAgeHours: number;
  health: BackupHealth;
  healthDetails: string | null;
  status: BackupStatus | null;
}
