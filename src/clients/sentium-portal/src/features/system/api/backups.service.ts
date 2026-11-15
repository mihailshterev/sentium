import { client } from "@/shared/api/client";
import type { BackupsOverview } from "@/features/system/types/backup";

const BASE = "/vigil/backups";

export const fetchBackups = (): Promise<BackupsOverview> => client.get<BackupsOverview>(BASE);

/** Starts a backup on the sidecar and returns at once; poll {@link fetchBackups} for the result. */
export const startBackup = (): Promise<void> => client.post<void>(`${BASE}/runs`, undefined);
