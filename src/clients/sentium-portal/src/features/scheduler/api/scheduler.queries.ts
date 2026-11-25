import { queryOptions } from "@tanstack/react-query";
import {
  fetchActiveSchedulerJobs,
  fetchBackgroundJobs,
  fetchPreApprovableTools,
  fetchScheduleTemplates,
} from "./scheduler.service";
import type { PreApprovalTarget } from "../types/scheduler";

/** Invalidating `jobs()` also refreshes the templates and pre-approvable tools, which live under it. */
export const schedulerKeys = {
  jobs: () => ["scheduler-jobs"] as const,
  templates: () => [...schedulerKeys.jobs(), "templates"] as const,
  preApprovableTools: (target: PreApprovalTarget) => [...schedulerKeys.jobs(), "pre-approvable", target] as const,
  background: () => ["scheduler-background-jobs"] as const,
};

export const schedulerQueries = {
  jobs: () => queryOptions({ queryKey: schedulerKeys.jobs(), queryFn: () => fetchActiveSchedulerJobs() }),
  templates: () =>
    queryOptions({ queryKey: schedulerKeys.templates(), queryFn: () => fetchScheduleTemplates(), staleTime: Infinity }),
  preApprovableTools: (target: PreApprovalTarget) =>
    queryOptions({
      queryKey: schedulerKeys.preApprovableTools(target),
      queryFn: () => fetchPreApprovableTools(target),
    }),
  background: () => queryOptions({ queryKey: schedulerKeys.background(), queryFn: () => fetchBackgroundJobs() }),
};
