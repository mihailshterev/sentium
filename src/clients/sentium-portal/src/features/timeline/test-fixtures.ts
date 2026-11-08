import type {
  MonthDetail,
  MonthSummary,
  TimelineNote,
  TimelineProposal,
  TimelineTask,
  Upcoming,
  YearOverview,
} from "./types/timeline";

export const task = (overrides: Partial<TimelineTask> = {}): TimelineTask => ({
  id: "task-1",
  title: "Book dentist",
  description: null,
  year: 2026,
  month: 11,
  date: "2026-11-12",
  dueDate: null,
  priority: "Normal",
  status: "Todo",
  completedAt: null,
  tags: [],
  assignedAgent: null,
  origin: "User",
  createdByAgent: null,
  noteCount: 0,
  createdAt: "2026-11-01T10:00:00Z",
  updatedAt: "2026-11-01T10:00:00Z",
  ...overrides,
});

export const note = (overrides: Partial<TimelineNote> = {}): TimelineNote => ({
  id: "note-1",
  year: 2026,
  month: 11,
  date: null,
  taskId: null,
  kind: "ProgressReport",
  author: "Agent",
  authorName: "TimelinePlanner",
  content: "Reviewed **November**.",
  createdAt: "2026-11-07T09:00:00Z",
  ...overrides,
});

export const proposal = (overrides: Partial<TimelineProposal> = {}): TimelineProposal => ({
  id: "proposal-1",
  kind: "CreateTask",
  status: "Pending",
  taskId: null,
  title: "Plan winter trip",
  description: null,
  priority: "Normal",
  tags: [],
  assignedAgent: null,
  dueDate: null,
  year: 2026,
  month: 11,
  date: "2026-11-20",
  fromYear: null,
  fromMonth: null,
  fromDate: null,
  rationale: "The 20th is free",
  proposedBy: "TimelinePlanner",
  createdAt: "2026-11-07T09:00:00Z",
  resolvedAt: null,
  ...overrides,
});

export const monthSummary = (month: number, overrides: Partial<MonthSummary> = {}): MonthSummary => ({
  month,
  taskCount: 0,
  openCount: 0,
  doneCount: 0,
  overdueCount: 0,
  agentAssignedCount: 0,
  noteCount: 0,
  agentNoteCount: 0,
  pendingProposalCount: 0,
  lastAgentActivityAt: null,
  ...overrides,
});

export const yearOverview = (year = 2026, overrides: Record<number, Partial<MonthSummary>> = {}): YearOverview => ({
  year,
  today: "2026-11-08",
  months: Array.from({ length: 12 }, (_, i) => monthSummary(i + 1, overrides[i + 1])),
  days: [],
});

export const upcoming = (overrides: Partial<Upcoming> = {}): Upcoming => ({
  today: "2026-11-08",
  days: 7,
  overdue: [],
  overdueCount: 0,
  next: [],
  pendingProposals: 0,
  ...overrides,
});

export const monthDetail = (overrides: Partial<MonthDetail> = {}): MonthDetail => ({
  year: 2026,
  month: 11,
  tasks: [],
  notes: [],
  proposals: [],
  ...overrides,
});
