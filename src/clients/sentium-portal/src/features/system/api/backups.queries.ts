import { queryOptions } from "@tanstack/react-query";
import { fetchBackups } from "./backups.service";

export const backupKeys = {
  overview: () => ["system", "backups"] as const,
};

export const backupQueries = {
  overview: () => queryOptions({ queryKey: backupKeys.overview(), queryFn: () => fetchBackups() }),
};
