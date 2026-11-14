import { client } from "@/shared/api/client";
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

const id = (value: string) => encodeURIComponent(value);

export const fetchYear = (year: number, today?: string) =>
  client.get<YearOverview>(`${BASE}/years/${year}${today ? `?today=${today}` : ""}`);

export const fetchMonth = (year: number, month: number, today?: string) =>
  client.get<MonthDetail>(`${BASE}/years/${year}/months/${month}${today ? `?today=${today}` : ""}`);

export const fetchUpcoming = (today: string, days = 7) =>
  client.get<Upcoming>(`${BASE}/upcoming?today=${today}&days=${days}`);

export const createTask = (payload: SaveTaskPayload) => client.post<TimelineTask>(`${BASE}/tasks`, payload);

export const updateTask = (taskId: string, payload: SaveTaskPayload, scope: EditScope = "One") =>
  client.put<TimelineTask>(`${BASE}/tasks/${id(taskId)}?scope=${scope}`, payload);

export const scheduleTask = (taskId: string, placement: TaskPlacement) =>
  client.put<TimelineTask>(`${BASE}/tasks/${id(taskId)}/schedule`, placement);

export const setTaskStatus = (taskId: string, status: TaskStatus) =>
  client.put<TimelineTask>(`${BASE}/tasks/${id(taskId)}/status`, { status });

export const deleteTask = (taskId: string, scope: EditScope = "One") =>
  client.delete<void>(`${BASE}/tasks/${id(taskId)}?scope=${scope}`);

export const createNote = (payload: CreateNotePayload) => client.post<TimelineNote>(`${BASE}/notes`, payload);

export const deleteNote = (noteId: string) => client.delete<void>(`${BASE}/notes/${id(noteId)}`);

export const fetchProposals = (status: ProposalStatus = "Pending") =>
  client.get<TimelineProposal[]>(`${BASE}/proposals?status=${status}`);

export const acceptProposal = (proposalId: string) =>
  client.post<TimelineTask>(`${BASE}/proposals/${id(proposalId)}/accept`, undefined);

export const rejectProposal = (proposalId: string) =>
  client.post<TimelineProposal>(`${BASE}/proposals/${id(proposalId)}/reject`, undefined);
