import { Archive, DatabaseBackup, Lock } from "lucide-react";
import { useBackups } from "@/features/system/hooks/useBackups";
import type { BackupsOverview, BackupStatus } from "@/features/system/types/backup";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { formatDateTimeShort, formatRelativeTime } from "@/shared/utils/formatters";
import { formatBytes } from "../system-utils";
import styles from "../system.module.css";

/** Sovereign-only: the verified backups at the destination, the last run, and "back up now". */
export function BackupsCard({ className }: { className?: string }) {
  const { overview, isLoading, error, start } = useBackups();
  const status = overview?.status ?? null;
  const running = start.isPending || !!status?.running;

  return (
    <Card padding="none" as="section" className={className} data-testid="backups-card">
      <CardHeader
        icon={<DatabaseBackup />}
        title="Backups"
        description={status ? describePolicy(status) : "Nightly backups of the database, files and keys."}
        actions={
          <>
            {overview && <HealthBadge overview={overview} />}
            <Button
              size="sm"
              icon={<DatabaseBackup />}
              loading={running}
              disabled={!status}
              onClick={() => start.mutate()}
              data-testid="backup-now"
            >
              {running ? "Backing up" : "Back up now"}
            </Button>
          </>
        }
      />

      {start.error && <StatusMessage variant="error" message={start.error.message} className={styles.backupMessage} />}

      {isLoading ? (
        <div className={styles.backupLoading} aria-busy="true">
          <SkeletonText lines={2} />
        </div>
      ) : error || !overview ? (
        <StatusMessage variant="error" message="Backups could not be loaded." className={styles.backupMessage} />
      ) : !overview.configured ? (
        <EmptyState
          compact
          icon={<DatabaseBackup />}
          title="Backups are not set up"
          hint="The backup service runs with the Docker Compose installation. See Backups in the installation guide."
        />
      ) : (
        <BackupsBody overview={overview} status={status} />
      )}
    </Card>
  );
}

function HealthBadge({ overview }: { overview: BackupsOverview }) {
  if (!overview.configured) {
    return <Badge tone="neutral">Not set up</Badge>;
  }

  return overview.health === "Healthy" ? (
    <Badge tone="success" dot data-testid="backup-health">
      Healthy
    </Badge>
  ) : (
    <Badge tone="danger" dot data-testid="backup-health">
      Needs attention
    </Badge>
  );
}

function BackupsBody({ overview, status }: { overview: BackupsOverview; status: BackupStatus | null }) {
  return (
    <>
      {overview.health !== "Healthy" && overview.healthDetails && (
        <StatusMessage variant="warning" message={overview.healthDetails} className={styles.backupMessage} />
      )}

      {status && <BackupList status={status} />}
    </>
  );
}

function BackupList({ status }: { status: BackupStatus }) {
  return (
    <>
      <dl className={styles.backupFacts}>
        <div>
          <dt>Last run</dt>
          <dd data-testid="backup-last-run">{describeLastRun(status)}</dd>
        </div>
        <div>
          <dt>Next run</dt>
          <dd>{status.nextRunAt ? formatDateTimeShort(status.nextRunAt) : "—"}</dd>
        </div>
      </dl>

      {status.backups.length === 0 ? (
        <EmptyState compact icon={<Archive />} title="No backups yet" />
      ) : (
        <ul className={styles.backupList}>
          {status.backups.map((backup) => (
            <li key={backup.name} className={styles.backupItem} data-testid="backup-item">
              <Archive size={16} className={styles.backupIcon} aria-hidden="true" />
              <span className={styles.backupName}>{formatDateTimeShort(backup.createdAt)}</span>
              <span className={styles.backupMeta}>{formatRelativeTime(backup.createdAt)}</span>
              {backup.encrypted && <Lock size={14} className={styles.backupIcon} aria-label="Encrypted" />}
              <span className={styles.backupSize}>{formatBytes(backup.sizeBytes)}</span>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

function describePolicy(status: BackupStatus): string {
  const keep = `Keeps ${status.keepDaily} daily and ${status.keepWeekly} weekly`;
  return status.encryptionEnabled ? `${keep}, encrypted.` : `${keep}.`;
}

function describeLastRun(status: BackupStatus): string {
  if (status.running) {
    return "Running now…";
  }

  const run = status.lastRun;
  if (!run) {
    return "None yet";
  }

  const when = formatRelativeTime(run.finishedAt);
  const how = run.trigger === "manual" ? "manual" : "scheduled";
  return run.succeeded ? `Succeeded ${when} (${how})` : `Failed ${when} (${how}): ${run.error ?? "unknown error"}`;
}
