import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchActiveSchedulerJobs, deleteScheduledJob } from "../services/scheduler.service";
import type { CronJobRecord } from "../types/scheduler";

const SCHEDULER_POLL_INTERVAL = 5_000;
const QUERY_KEY = "scheduler-jobs";

export const useSchedulerJobs = () => {
  const {
    data: jobs = [],
    isLoading,
    error,
    refetch,
  } = useQuery<CronJobRecord[]>({
    queryKey: [QUERY_KEY],
    queryFn: fetchActiveSchedulerJobs,
    refetchInterval: SCHEDULER_POLL_INTERVAL,
    retry: false,
  });

  return { jobs, isLoading, error, refetch };
};

export const useDeleteJobMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ agentId, jobId }: { agentId: string; jobId: string }) => deleteScheduledJob(agentId, jobId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] });
    },
  });
};
