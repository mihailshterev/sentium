import { queryOptions } from "@tanstack/react-query";
import { fetchAuditStats } from "./sentinel.service";

export const sentinelKeys = {
  audit: () => ["sentinel-audit"] as const,
  stats: () => ["sentinel-stats"] as const,
};

export const sentinelQueries = {
  stats: () => queryOptions({ queryKey: sentinelKeys.stats(), queryFn: () => fetchAuditStats() }),
};
