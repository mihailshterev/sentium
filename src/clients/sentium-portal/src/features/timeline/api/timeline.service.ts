import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type {
  CreateNotePayload,
  EditScope,
  MonthDetail,
  ProposalStatus,
  SaveTaskPayload,
  TaskPlacement,
  TaskStatus,
  TimelineNote,
  TimelineProposal,
  TimelineTask,
  Upcoming,
  YearOverview,
} from "../types/timeline";

const BASE = "/tempus";
const url = pathIn(BASE);

export const fetchYear = (year: number, today?: string) =>
  client.get<YearOverview>(withQuery(url`/years/${year}`, { today }));

export const fetchMonth = (year: number, month: number, today?: string) =>
  client.get<MonthDetail>(withQuery(url`/years/${year}/months/${month}`, { today }));

export const fetchUpcoming = (today: string, days = 7) =>
  client.get<Upcoming>(withQuery(`${BASE}/upcoming`, { today, days }));

export const createTask = (payload: SaveTaskPayload) => client.post<TimelineTask>(`${BASE}/tasks`, payload);

export const updateTask = (taskId: string, payload: SaveTaskPayload, scope: EditScope = "One") =>
  client.put<TimelineTask>(withQuery(url`/tasks/${taskId}`, { scope }), payload);

export const scheduleTask = (taskId: string, placement: TaskPlacement) =>
  client.put<TimelineTask>(url`/tasks/${taskId}/schedule`, placement);

export const setTaskStatus = (taskId: string, status: TaskStatus) =>
  client.put<TimelineTask>(url`/tasks/${taskId}/status`, { status });

export const deleteTask = (taskId: string, scope: EditScope = "One") =>
  client.delete<void>(withQuery(url`/tasks/${taskId}`, { scope }));

export const createNote = (payload: CreateNotePayload) => client.post<TimelineNote>(`${BASE}/notes`, payload);

export const deleteNote = (noteId: string) => client.delete<void>(url`/notes/${noteId}`);

export const fetchProposals = (status: ProposalStatus = "Pending") =>
  client.get<TimelineProposal[]>(withQuery(`${BASE}/proposals`, { status }));

export const acceptProposal = (proposalId: string) =>
  client.post<TimelineTask>(url`/proposals/${proposalId}/accept`, undefined);

export const rejectProposal = (proposalId: string) =>
  client.post<TimelineProposal>(url`/proposals/${proposalId}/reject`, undefined);
