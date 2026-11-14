import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { BASE_URL } from "@/shared/api/client";
import type { NotificationStreamFrame } from "@/features/notifications/types/notification";
import { notificationKeys } from "./useInbox";

const REFRESHING_FRAMES = new Set<string>(["connected", "notification", "read"]);

/**
 * Keeps the inbox queries live. The stream only carries what happens while connected, so the "connected" frame
 * (sent again after every reconnect) refetches too: a frame missed while offline costs nothing.
 */
export function useNotificationStream() {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (typeof EventSource === "undefined") {
      return;
    }

    const source = new EventSource(`${BASE_URL}/nuntius/stream`, { withCredentials: true });

    source.onmessage = (event) => {
      let frame: NotificationStreamFrame;
      try {
        frame = JSON.parse(event.data);
      } catch {
        return;
      }

      if (REFRESHING_FRAMES.has(frame.type)) {
        queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      }
    };

    return () => source.close();
  }, [queryClient]);
}
