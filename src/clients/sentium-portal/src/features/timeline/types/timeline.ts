export type TaskPriority = "Low" | "Normal" | "High" | "Urgent";
export type TaskStatus = "Todo" | "InProgress" | "Done";
export type AuthorKind = "User" | "Agent";
export type NoteKind = "Note" | "StatusUpdate" | "ProgressReport";
export type ProposalKind = "CreateTask" | "Reschedule" | "CreateEvent";
/** A Task is ticked done and can be overdue; a Calendar Event (labelled "Event") just happens and is past once over. */
export type EntryKind = "Task" | "CalendarEvent";
export type RepeatFrequency = "Daily" | "Weekly" | "Monthly" | "Yearly";
/** Which occurrences of a Series an edit or delete applies to. */
export type EditScope = "One" | "Following";

/** A Series rule. `weekdays` are .NET `DayOfWeek` numbers (0 = Sunday), weekly only. */
export interface Repeat {
  frequency: RepeatFrequency;
  interval: number;
  weekdays: number[] | null;
  until: string | null;
}
export type ProposalStatus = "Pending" | "Accepted" | "Rejected" | "Superseded";

/**
 * An Entry (Task or Calendar Event) on a day (`date`), or pinned to its month (`date` null). Dates are `YYYY-MM-DD`,
 * times `HH:mm:ss`. `isPast` is set by the server for a Calendar Event that is over; Tasks are never past.
 */
export interface TimelineTask {
  id: string;
  kind: EntryKind;
  title: string;
  description: string | null;
  year: number;
  month: number;
  date: string | null;
  startTime: string | null;
  endDate: string | null;
  dueDate: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  completedAt: string | null;
  tags: string[];
  assignedAgent: string | null;
  origin: AuthorKind;
  createdByAgent: string | null;
  noteCount: number;
  seriesId: string | null;
  occurrenceDate: string | null;
  isDetached: boolean;
  repeat: Repeat | null;
  reminderOffsetMinutes: number | null;
  isPast: boolean;
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
  startTime: string | null;
  endDate: string | null;
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
  /** Calendar Events; every other count is Tasks only. */
  eventCount: number;
}

export interface DaySummary {
  date: string;
  open: number;
  done: number;
  urgent: boolean;
  events: number;
}

export interface YearOverview {
  year: number;
  today: string;
  months: MonthSummary[];
  days: DaySummary[];
}

export interface Upcoming {
  today: string;
  days: number;
  overdue: TimelineTask[];
  overdueCount: number;
  next: TimelineTask[];
  pendingProposals: number;
  events: TimelineTask[];
}

export interface MonthDetail {
  year: number;
  month: number;
  tasks: TimelineTask[];
  notes: TimelineNote[];
  proposals: TimelineProposal[];
}

/** Task-only fields (`dueDate`, `priority`, `status`, `assignedAgent`) must be null for a Calendar Event. */
export interface SaveTaskPayload {
  kind: EntryKind;
  title: string;
  description: string | null;
  year: number;
  month: number;
  date: string | null;
  startTime: string | null;
  endDate: string | null;
  dueDate: string | null;
  priority: TaskPriority | null;
  status: TaskStatus | null;
  tags: string[];
  assignedAgent: string | null;
  repeat: Repeat | null;
  reminderOffsetMinutes: number | null;
}

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

export type AgentChatContext = { kind: "task"; task: TimelineTask } | { kind: "month"; year: number; month: number };
