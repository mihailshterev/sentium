import { useEffect, useRef } from "react";
import { useQueryClient, type QueryKey } from "@tanstack/react-query";
import {
  startRealtime,
  stopRealtime,
  subscribeTopic,
  useRealtimeStatusStore,
  type RealtimeMessage,
} from "./realtime-client";

/**
 * Events often come in bursts (an Evaluation's cases, Sentinel decisions, an inventory re-sync); one refetch covers a
 * burst. Long enough that a refetch usually finishes before the next one is due.
 */
const INVALIDATE_COALESCE_MS = 1_000;

/** Keeps the realtime stream open while mounted; mount it once, in the signed-in layout. */
export function useRealtimeConnection(): void {
  useEffect(() => {
    startRealtime();
    return () => stopRealtime();
  }, []);
}

export function useRealtimeStatus() {
  return useRealtimeStatusStore((s) => s.status);
}

/** Calls the latest `handler` for every event on `topics`, and with `resync` after every (re)connect. */
export function useRealtimeTopic(
  topics: string | readonly string[],
  handler: (message: RealtimeMessage) => void,
): void {
  const handlerRef = useRef(handler);
  useEffect(() => {
    handlerRef.current = handler;
  });

  const topicKey = typeof topics === "string" ? topics : topics.join("|");
  useEffect(() => subscribeTopic(topicKey.split("|"), (message) => handlerRef.current(message)), [topicKey]);
}

/**
 * Refetches `queryKeys` whenever `topics` report a change, and after every (re)connect - the events are hints, the
 * REST response stays the truth.
 */
export function useRealtimeInvalidate(topics: string | readonly string[], queryKeys: readonly QueryKey[]): void {
  const queryClient = useQueryClient();
  const keysRef = useRef(queryKeys);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    keysRef.current = queryKeys;
  });

  useEffect(
    () => () => {
      if (timer.current) {
        clearTimeout(timer.current);
      }
    },
    [],
  );

  useRealtimeTopic(topics, () => {
    if (timer.current) {
      return;
    }
    timer.current = setTimeout(() => {
      timer.current = null;
      for (const queryKey of keysRef.current) {
        // A refetch already running is left to finish: cancelling it on every event of a burst would mean a slow
        // list never finishes loading while the burst lasts.
        void queryClient.invalidateQueries({ queryKey }, { cancelRefetch: false });
      }
    }, INVALIDATE_COALESCE_MS);
  });
}
