import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { DEVICE_STATUSES_KEY, type DeviceStatus, type DeviceUpdate } from "@/features/devices";
import { BASE_URL } from "@/shared/api/client";
import { SERVICE_HEALTH_KEY } from "./useServiceHealth";
import { SYSTEM_OVERVIEW_KEY } from "./useSystemOverview";
import { INCIDENTS_KEY } from "./useIncidents";
import type { ComponentKind, ServiceHealthStatus, ServiceStatus } from "@/features/vigil/types/serviceHealth";

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
  data?: unknown;
}

export function useVigilStream() {
  const queryClient = useQueryClient();
  const [isLive, setIsLive] = useState(false);
  const sourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const source = new EventSource(`${BASE_URL}/vigil/stream`, { withCredentials: true });
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
        const update = envelope.data as StatusPayload;
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
      } else if (envelope.type === "device.updated" && envelope.data) {
        const update = envelope.data as DeviceUpdate;
        const known = queryClient.getQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY)?.some((d) => d.id === update.id);
        if (!known) {
          // A Device paired since the list was loaded: the frame has no name or owner, so fetch the list.
          queryClient.invalidateQueries({ queryKey: DEVICE_STATUSES_KEY });
          return;
        }
        queryClient.setQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY, (prev) =>
          prev?.map((d) =>
            d.id === update.id
              ? { ...d, isOnline: update.isOnline, lastSeenAt: update.lastSeenAt, report: update.report }
              : d,
          ),
        );
      } else if (envelope.type === "device.removed" && envelope.data) {
        const removedId = (envelope.data as { id: string }).id;
        queryClient.setQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY, (prev) => prev?.filter((d) => d.id !== removedId));
      }
    };

    return () => {
      source.close();
      sourceRef.current = null;
      setIsLive(false);
    };
  }, [queryClient]);

  return { isLive };
}
