import { queryOptions } from "@tanstack/react-query";
import { fetchNotifications, fetchUnreadCount } from "./notifications.service";

export const notificationKeys = {
  all: ["notifications"] as const,
  list: (unreadOnly: boolean, limit: number) => [...notificationKeys.all, "list", { unreadOnly, limit }] as const,
  unreadCount: () => [...notificationKeys.all, "unread-count"] as const,
};

export const notificationQueries = {
  list: (unreadOnly: boolean, limit: number) =>
    queryOptions({
      queryKey: notificationKeys.list(unreadOnly, limit),
      queryFn: () => fetchNotifications(unreadOnly, limit),
    }),
  unreadCount: () => queryOptions({ queryKey: notificationKeys.unreadCount(), queryFn: () => fetchUnreadCount() }),
};
