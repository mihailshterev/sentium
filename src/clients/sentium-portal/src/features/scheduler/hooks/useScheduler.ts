import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSchedule,
  deleteSchedule,
  fetchActiveSchedulerJobs,
  fetchBackgroundJobs,
  fetchPreApprovableTools,
  fetchScheduleTemplates,
  pauseSchedule,
  resumeSchedule,
  updateSchedule,
} from "@/features/scheduler/api/scheduler.service";
import type { CronJobRecord, PreApprovalTarget, SaveSchedulePayload } from "@/features/scheduler/types/scheduler";
import { REALTIME_FALLBACK_POLL_MS, REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";

const SCHEDULER_POLL_INTERVAL = 5_000;
const QUERY_KEY = "scheduler-jobs";
const BACKGROUND_KEY = "scheduler-background-jobs";
const JOB_KEYS = [[QUERY_KEY], [BACKGROUND_KEY]] as const;

export const useSchedulerJobs = () => {
  useRealtimeInvalidate(REALTIME_TOPICS.schedules, JOB_KEYS);

  const {
    data: jobs = [],
    isLoading,
    error,
    refetch,
  } = useQuery<CronJobRecord[]>({
    queryKey: [QUERY_KEY],
    queryFn: fetchActiveSchedulerJobs,
    refetchInterval: REALTIME_FALLBACK_POLL_MS,
    retry: false,
  });

  return { jobs, isLoading, error, refetch };
};

const useInvalidateJobs = () => {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
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
  const { data = [] } = useQuery({
    queryKey: [QUERY_KEY, "templates"],
    queryFn: fetchScheduleTemplates,
    staleTime: Infinity,
  });
  return data;
};

export const usePreApprovableTools = (target: PreApprovalTarget | null) => {
  const { data = [], isFetching, isError } = useQuery({
    queryKey: [QUERY_KEY, "pre-approvable", target],
    queryFn: () => fetchPreApprovableTools(target as PreApprovalTarget),
    enabled: target !== null,
    retry: false,
  });
  return { tools: target ? data : [], isLoading: isFetching, isError };
};

export const useBackgroundJobs = (enabled: boolean) => {
  const { data, isLoading, error } = useQuery({
    queryKey: [BACKGROUND_KEY],
    queryFn: fetchBackgroundJobs,
    enabled,
    refetchInterval: SCHEDULER_POLL_INTERVAL,
    retry: false,
  });
  return { snapshot: data, isLoading, error };
};
