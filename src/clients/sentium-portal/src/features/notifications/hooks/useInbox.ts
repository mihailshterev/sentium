import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  fetchNotifications,
  fetchUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
} from "@/features/notifications/api/notifications.service";

export const notificationKeys = {
  all: ["notifications"] as const,
  list: (unreadOnly: boolean, limit: number) => ["notifications", "list", { unreadOnly, limit }] as const,
  unreadCount: ["notifications", "unread-count"] as const,
};

export function useNotifications(unreadOnly = false, limit = 50) {
  const { data = [], isLoading, error } = useQuery({
    queryKey: notificationKeys.list(unreadOnly, limit),
    queryFn: () => fetchNotifications(unreadOnly, limit),
  });

  return { notifications: data, isLoading, error };
}

export function useUnreadCount() {
  const { data } = useQuery({
    queryKey: notificationKeys.unreadCount,
    queryFn: fetchUnreadCount,
  });

  return data?.count ?? 0;
}

export function useInboxActions() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: notificationKeys.all });

  const markRead = useMutation({ mutationFn: markNotificationRead, onSuccess: refresh });
  const markAllRead = useMutation({ mutationFn: markAllNotificationsRead, onSuccess: refresh });

  return {
    markRead: markRead.mutate,
    markAllRead: markAllRead.mutate,
    isMarkingAll: markAllRead.isPending,
  };
}
