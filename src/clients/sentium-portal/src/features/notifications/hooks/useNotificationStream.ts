import { REALTIME_TOPICS, useRealtimeInvalidate } from "@/shared/realtime";
import { notificationKeys } from "./useInbox";

const INBOX_KEYS = [notificationKeys.all] as const;

/**
 * Keeps the inbox queries live from the realtime stream. The stream only carries what happens while connected, so
 * every (re)connect refetches too: a notification missed while offline costs nothing.
 */
export function useNotificationStream() {
  useRealtimeInvalidate(REALTIME_TOPICS.notifications, INBOX_KEYS);
}
