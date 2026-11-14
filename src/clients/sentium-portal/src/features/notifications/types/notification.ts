export type NotificationSeverity = "Info" | "Warning" | "Critical";

export interface Notification {
  id: string;
  category: string;
  severity: NotificationSeverity;
  title: string;
  body: string | null;
  /** Portal path the notification opens, e.g. `/vigil`. */
  link: string | null;
  /** How many publishes this notification absorbed (1 for a fresh one). */
  occurrences: number;
  createdAt: string;
  updatedAt: string;
  readAt: string | null;
}

export interface UnreadCount {
  count: number;
}

export interface NotificationStreamFrame {
  type: "connected" | "notification" | "read";
  data?: Notification;
}
