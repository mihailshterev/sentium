import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
  type QueryClient,
  type QueryKey,
} from "@tanstack/react-query";
import * as api from "../api/timeline.service";
import type {
  CreateNotePayload,
  EditScope,
  MonthDetail,
  SaveTaskPayload,
  TaskPlacement,
  TaskStatus,
  TimelineProposal,
  TimelineTask,
  YearOverview,
} from "../types/timeline";
import { withTask, withoutTask } from "../utils/month-cache";

export const timelineKeys = {
  all: ["timeline"] as const,
  year: (year: number) => ["timeline", "year", year] as const,
  month: (year: number, month: number) => ["timeline", "month", year, month] as const,
  proposals: () => ["timeline", "proposals"] as const,
  upcoming: () => ["timeline", "upcoming"] as const,
};

/** Agents can write from workflows and schedules too, so the open timeline refreshes itself now and then. */
const REFRESH_MS = 60_000;

/** The overviews of every year the scrollable rail shows, keyed by year (loaded ones only). */
export function useYearOverviews(years: number[], today: string) {
  return useQueries({
    queries: years.map((year) => ({
      queryKey: timelineKeys.year(year),
      queryFn: () => api.fetchYear(year, today),
      refetchInterval: REFRESH_MS,
    })),
    combine: (results) => ({
      overviews: new Map<number, YearOverview>(results.flatMap((r) => (r.data ? [[r.data.year, r.data]] : []))),
      isError: results.some((r) => r.isError),
    }),
  });
}

export function useUpcoming(today: string) {
  const { data, isLoading, isError } = useQuery({
    queryKey: timelineKeys.upcoming(),
    queryFn: () => api.fetchUpcoming(today),
    refetchInterval: REFRESH_MS,
  });
  return { upcoming: data, isLoading, isError };
}

export function useMonthDetail(year: number, month: number | null, today?: string) {
  const { data, isLoading, isError } = useQuery({
    queryKey: timelineKeys.month(year, month ?? 0),
    queryFn: () => api.fetchMonth(year, month!, today),
    enabled: month !== null,
    refetchInterval: REFRESH_MS,
  });
  return { detail: data, isLoading, isError };
}

export function usePendingProposals() {
  const { data, isLoading } = useQuery({
    queryKey: timelineKeys.proposals(),
    queryFn: () => api.fetchProposals("Pending"),
    refetchInterval: REFRESH_MS,
  });
  return { proposals: data ?? [], isLoading };
}

type Snapshot = [QueryKey, MonthDetail | undefined][];

async function snapshotMonths(qc: QueryClient, keys: QueryKey[]): Promise<Snapshot> {
  await Promise.all(keys.map((key) => qc.cancelQueries({ queryKey: key })));
  return keys.map((key) => [key, qc.getQueryData<MonthDetail>(key)]);
}

const restore = (qc: QueryClient, snapshot: Snapshot | undefined) =>
  snapshot?.forEach(([key, data]) => qc.setQueryData(key, data));

function useInvalidateTimeline() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: timelineKeys.all });
}

export function useTaskMutations() {
  const qc = useQueryClient();
  const invalidate = useInvalidateTimeline();

  const create = useMutation({
    mutationFn: (payload: SaveTaskPayload) => api.createTask(payload),
    onSuccess: invalidate,
  });

  const update = useMutation({
    mutationFn: ({ id, payload, scope }: { id: string; payload: SaveTaskPayload; scope?: EditScope }) =>
      api.updateTask(id, payload, scope),
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: ({ id, scope }: { id: string; scope?: EditScope }) => api.deleteTask(id, scope),
    onSuccess: invalidate,
  });

  /** Drag and drop: the chip lands on its new day at once and snaps back if the server refuses. */
  const schedule = useMutation({
    mutationFn: ({ task, target }: { task: TimelineTask; target: TaskPlacement }) => api.scheduleTask(task.id, target),
    onMutate: async ({ task, target }) => {
      const source = timelineKeys.month(task.year, task.month);
      const destination = timelineKeys.month(target.year, target.month);
      const snapshot = await snapshotMonths(qc, [source, destination]);
      const moved: TimelineTask = { ...task, ...target };

      qc.setQueryData<MonthDetail>(source, (d) => d && withoutTask(d, task.id));
      qc.setQueryData<MonthDetail>(destination, (d) => d && withTask(d, moved));
      return { snapshot };
    },
    onError: (_error, _vars, context) => restore(qc, context?.snapshot),
    onSettled: invalidate,
  });

  const setStatus = useMutation({
    mutationFn: ({ task, status }: { task: TimelineTask; status: TaskStatus }) => api.setTaskStatus(task.id, status),
    onMutate: async ({ task, status }) => {
      const key = timelineKeys.month(task.year, task.month);
      const snapshot = await snapshotMonths(qc, [key]);
      const completedAt = status === "Done" ? (task.completedAt ?? new Date().toISOString()) : null;
      qc.setQueryData<MonthDetail>(key, (d) => d && withTask(d, { ...task, status, completedAt }));
      return { snapshot };
    },
    onError: (_error, _vars, context) => restore(qc, context?.snapshot),
    onSettled: invalidate,
  });

  return { create, update, remove, schedule, setStatus };
}

export function useNoteMutations() {
  const invalidate = useInvalidateTimeline();

  const create = useMutation({
    mutationFn: (payload: CreateNotePayload) => api.createNote(payload),
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.deleteNote(id),
    onSuccess: invalidate,
  });

  return { create, remove };
}

export function useProposalMutations() {
  const invalidate = useInvalidateTimeline();

  // Settled, not success: a stale proposal answers 409 but is superseded server-side, so it must disappear too.
  const accept = useMutation({
    mutationFn: (proposal: TimelineProposal) => api.acceptProposal(proposal.id),
    onSettled: invalidate,
  });

  const reject = useMutation({
    mutationFn: (proposal: TimelineProposal) => api.rejectProposal(proposal.id),
    onSettled: invalidate,
  });

  return { accept, reject };
}
