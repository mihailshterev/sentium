import { queryOptions } from "@tanstack/react-query";
import { fetchMonth, fetchProposals, fetchUpcoming, fetchYear } from "./timeline.service";

/** `today` only shapes the server's "overdue" flags, so it stays out of the keys. */
export const timelineKeys = {
  all: ["timeline"] as const,
  year: (year: number) => [...timelineKeys.all, "year", year] as const,
  month: (year: number, month: number) => [...timelineKeys.all, "month", year, month] as const,
  proposals: () => [...timelineKeys.all, "proposals"] as const,
  upcoming: () => [...timelineKeys.all, "upcoming"] as const,
};

export const timelineQueries = {
  year: (year: number, today: string) =>
    queryOptions({ queryKey: timelineKeys.year(year), queryFn: () => fetchYear(year, today) }),
  month: (year: number, month: number, today?: string) =>
    queryOptions({ queryKey: timelineKeys.month(year, month), queryFn: () => fetchMonth(year, month, today) }),
  upcoming: (today: string) => queryOptions({ queryKey: timelineKeys.upcoming(), queryFn: () => fetchUpcoming(today) }),
  pendingProposals: () =>
    queryOptions({ queryKey: timelineKeys.proposals(), queryFn: () => fetchProposals("Pending") }),
};
