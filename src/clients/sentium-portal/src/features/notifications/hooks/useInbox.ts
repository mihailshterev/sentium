import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { markAllNotificationsRead, markNotificationRead } from "@/features/notifications/api/notifications.service";
import { notificationKeys, notificationQueries } from "@/features/notifications/api/notifications.queries";

export function useNotifications(unreadOnly = false, limit = 50) {
  const { data = [], isLoading, error } = useQuery(notificationQueries.list(unreadOnly, limit));

  return { notifications: data, isLoading, error };
}

export function useUnreadCount() {
  const { data } = useQuery(notificationQueries.unreadCount());

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
