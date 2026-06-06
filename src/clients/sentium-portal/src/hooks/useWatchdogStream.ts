import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { BASE_URL } from "../api/client";
import { SERVICE_HEALTH_KEY } from "./useServiceHealth";
import { SYSTEM_OVERVIEW_KEY } from "./useSystemOverview";
import { INCIDENTS_KEY } from "./useIncidents";
import type { ComponentKind, ServiceHealthStatus, ServiceStatus } from "../types/serviceHealth";

interface StatusPayload {
  serviceName: string;
  kind: ComponentKind;
  status: ServiceStatus;
  latencyMs: number;
  uptimePercent: number;
  timestamp: string;
  details?: string | null;
}

interface Envelope {
  type: string;
  data?: StatusPayload;
}

const useWatchdogStream = () => {
  const queryClient = useQueryClient();
  const [isLive, setIsLive] = useState(false);
  const sourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const source = new EventSource(`${BASE_URL}/watchdog/stream`, { withCredentials: true });
    sourceRef.current = source;

    source.onopen = () => setIsLive(true);
    source.onerror = () => setIsLive(false);

    source.onmessage = (event) => {
      if (!event.data || event.data.startsWith(":")) {
        return;
      }

      let envelope: Envelope;
      try {
        envelope = JSON.parse(event.data);
      } catch {
        return;
      }

      if (envelope.type === "status" && envelope.data) {
        const update = envelope.data;
        queryClient.setQueryData<ServiceHealthStatus[]>(SERVICE_HEALTH_KEY, (prev) => {
          if (!prev) {
            return prev;
          }
          return prev.map((s) =>
            s.serviceName === update.serviceName
              ? {
                  ...s,
                  status: update.status,
                  latencyMs: update.latencyMs,
                  uptimePercent: update.uptimePercent,
                  checkedAt: update.timestamp,
                  details: update.details ?? null,
                }
              : s,
          );
        });
        queryClient.invalidateQueries({ queryKey: SYSTEM_OVERVIEW_KEY });
      } else if (envelope.type === "incident.opened" || envelope.type === "incident.resolved") {
        queryClient.invalidateQueries({ queryKey: INCIDENTS_KEY });
        queryClient.invalidateQueries({ queryKey: SYSTEM_OVERVIEW_KEY });
      }
    };

    return () => {
      source.close();
      sourceRef.current = null;
      setIsLive(false);
    };
  }, [queryClient]);

  return { isLive };
};

export default useWatchdogStream;
