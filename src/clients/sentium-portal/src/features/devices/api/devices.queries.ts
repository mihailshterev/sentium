import { queryOptions } from "@tanstack/react-query";
import { fetchDeviceStatuses, fetchMyDevices } from "./devices.service";

export const deviceKeys = {
  all: ["devices"] as const,
  mine: () => [...deviceKeys.all, "mine"] as const,
  statuses: () => [...deviceKeys.all, "statuses"] as const,
};

export const deviceQueries = {
  mine: () => queryOptions({ queryKey: deviceKeys.mine(), queryFn: () => fetchMyDevices() }),
  /** The Vigil stream patches this cache between polls. */
  statuses: () => queryOptions({ queryKey: deviceKeys.statuses(), queryFn: () => fetchDeviceStatuses() }),
};
