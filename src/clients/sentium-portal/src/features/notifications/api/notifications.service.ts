import { client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { Notification, UnreadCount } from "@/features/notifications/types/notification";

const BASE = "/nuntius/notifications";
const url = pathIn(BASE);

export const fetchNotifications = (unreadOnly = false, limit = 50): Promise<Notification[]> =>
  client.get<Notification[]>(withQuery(BASE, { unreadOnly, limit }));

export const fetchUnreadCount = (): Promise<UnreadCount> => client.get<UnreadCount>(`${BASE}/unread-count`);

export const markNotificationRead = (id: string): Promise<void> =>
  client.post<void>(url`/${id}/read`, undefined);

export const markAllNotificationsRead = (): Promise<UnreadCount> => client.post<UnreadCount>(`${BASE}/read-all`, undefined);
