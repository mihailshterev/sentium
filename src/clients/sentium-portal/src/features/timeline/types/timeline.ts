export type TaskPriority = "Low" | "Normal" | "High" | "Urgent";
export type TaskStatus = "Todo" | "InProgress" | "Done";
export type AuthorKind = "User" | "Agent";
export type NoteKind = "Note" | "StatusUpdate" | "ProgressReport";
export type ProposalKind = "CreateTask" | "Reschedule";
export type ProposalStatus = "Pending" | "Accepted" | "Rejected" | "Superseded";

/** A task on a day (`date`), or pinned to its month (`date` null). Dates are `YYYY-MM-DD`. */
export interface TimelineTask {
  id: string;
  title: string;
  description: string | null;
  year: number;
  month: number;
  date: string | null;
  dueDate: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  completedAt: string | null;
  tags: string[];
  assignedAgent: string | null;
  origin: AuthorKind;
  createdByAgent: string | null;
  noteCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface TimelineNote {
  id: string;
  year: number;
  month: number;
  date: string | null;
  taskId: string | null;
  kind: NoteKind;
  author: AuthorKind;
  authorName: string | null;
  content: string;
  createdAt: string;
}

export interface TimelineProposal {
  id: string;
  kind: ProposalKind;
  status: ProposalStatus;
  taskId: string | null;
  title: string;
  description: string | null;
  priority: TaskPriority;
  tags: string[];
  assignedAgent: string | null;
  dueDate: string | null;
  year: number;
  month: number;
  date: string | null;
  fromYear: number | null;
  fromMonth: number | null;
  fromDate: string | null;
  rationale: string | null;
  proposedBy: string;
  createdAt: string;
  resolvedAt: string | null;
}

export interface MonthSummary {
  month: number;
  taskCount: number;
  openCount: number;
  doneCount: number;
  overdueCount: number;
  agentAssignedCount: number;
  noteCount: number;
  agentNoteCount: number;
  pendingProposalCount: number;
  lastAgentActivityAt: string | null;
}

export interface YearOverview {
  year: number;
  today: string;
  months: MonthSummary[];
}

export interface MonthDetail {
  year: number;
  month: number;
  tasks: TimelineTask[];
  notes: TimelineNote[];
  proposals: TimelineProposal[];
}

export interface SaveTaskPayload {
  title: string;
  description: string | null;
  year: number;
  month: number;
  date: string | null;
  dueDate: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  tags: string[];
  assignedAgent: string | null;
}

/** A day, or a month without a day. */
export interface TaskPlacement {
  year: number;
  month: number;
  date: string | null;
}

export interface CreateNotePayload {
  year: number;
  month: number;
  date: string | null;
  taskId: string | null;
  content: string;
}

/** What the agent drawer is about: one task, or a whole month. */
export type AgentChatContext = { kind: "task"; task: TimelineTask } | { kind: "month"; year: number; month: number };
