import { client } from "@/shared/api/client";
import type { Notification, UnreadCount } from "@/features/notifications/types/notification";

const BASE = "/nuntius/notifications";

export const fetchNotifications = (unreadOnly = false, limit = 50): Promise<Notification[]> =>
  client.get<Notification[]>(`${BASE}?unreadOnly=${unreadOnly}&limit=${limit}`);

export const fetchUnreadCount = (): Promise<UnreadCount> => client.get<UnreadCount>(`${BASE}/unread-count`);

export const markNotificationRead = (id: string): Promise<void> =>
  client.post<void>(`${BASE}/${encodeURIComponent(id)}/read`, undefined);

export const markAllNotificationsRead = (): Promise<UnreadCount> => client.post<UnreadCount>(`${BASE}/read-all`, undefined);
