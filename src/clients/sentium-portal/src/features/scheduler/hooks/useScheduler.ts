import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSchedule,
  deleteSchedule,
  pauseSchedule,
  resumeSchedule,
  updateSchedule,
} from "@/features/scheduler/api/scheduler.service";
import { schedulerKeys, schedulerQueries } from "@/features/scheduler/api/scheduler.queries";
import type { PreApprovalTarget, SaveSchedulePayload } from "@/features/scheduler/types/scheduler";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const SCHEDULER_POLL_INTERVAL = 5_000;
const JOB_KEYS = [schedulerKeys.jobs(), schedulerKeys.background()] as const;

export const useSchedulerJobs = () => {
  useRealtimeInvalidate(REALTIME_TOPICS.schedules, JOB_KEYS);

  const {
    data: jobs = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    ...schedulerQueries.jobs(),
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { jobs, isLoading, error, refetch };
};

const useInvalidateJobs = () => {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: schedulerKeys.jobs() });
};

export const useDeleteJobMutation = () => {
  const onSuccess = useInvalidateJobs();
  return useMutation({ mutationFn: (jobId: string) => deleteSchedule(jobId), onSuccess });
};

export const useSaveScheduleMutation = () => {
  const onSuccess = useInvalidateJobs();
  return useMutation({
    mutationFn: ({ jobId, payload }: { jobId?: string; payload: SaveSchedulePayload }) =>
      jobId ? updateSchedule(jobId, payload) : createSchedule(payload),
    onSuccess,
  });
};

export const usePauseResumeMutation = () => {
  const onSuccess = useInvalidateJobs();
  return useMutation({
    mutationFn: ({ jobId, pause }: { jobId: string; pause: boolean }) =>
      pause ? pauseSchedule(jobId) : resumeSchedule(jobId),
    onSuccess,
  });
};

export const useScheduleTemplates = () => {
  const { data = [] } = useQuery(schedulerQueries.templates());
  return data;
};

export const usePreApprovableTools = (target: PreApprovalTarget | null) => {
  const { data = [], isFetching, isError } = useQuery({
    ...schedulerQueries.preApprovableTools(target as PreApprovalTarget),
    enabled: target !== null,
    retry: false,
  });
  return { tools: target ? data : [], isLoading: isFetching, isError };
};

export const useBackgroundJobs = (enabled: boolean) => {
  const { data, isLoading, error } = useQuery({
    ...schedulerQueries.background(),
    enabled,
    refetchInterval: SCHEDULER_POLL_INTERVAL,
    retry: false,
  });
  return { snapshot: data, isLoading, error };
};
