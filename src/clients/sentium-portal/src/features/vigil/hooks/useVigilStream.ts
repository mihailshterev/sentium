import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { DEVICE_STATUSES_KEY, type DeviceStatus, type DeviceUpdate } from "@/features/devices";
import { REALTIME_TOPICS, useRealtimeStatus, useRealtimeTopic, type RealtimeMessage } from "@/shared/realtime";
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

const VIGIL_TOPICS = [
  REALTIME_TOPICS.vigilStatus,
  REALTIME_TOPICS.vigilIncidentOpened,
  REALTIME_TOPICS.vigilIncidentResolved,
  REALTIME_TOPICS.vigilDeviceUpdated,
  REALTIME_TOPICS.vigilDeviceRemoved,
] as const;

function applyStatus(queryClient: QueryClient, update: StatusPayload) {
  queryClient.setQueryData<ServiceHealthStatus[]>(SERVICE_HEALTH_KEY, (prev) =>
    prev?.map((s) =>
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
    ),
  );
  void queryClient.invalidateQueries({ queryKey: SYSTEM_OVERVIEW_KEY }, { cancelRefetch: false });
}

function applyDeviceUpdate(queryClient: QueryClient, update: DeviceUpdate) {
  const known = queryClient.getQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY)?.some((d) => d.id === update.id);
  if (!known) {
    // A Device paired since the list was loaded: the frame has no name or owner, so fetch the list.
    void queryClient.invalidateQueries({ queryKey: DEVICE_STATUSES_KEY });
    return;
  }
  queryClient.setQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY, (prev) =>
    prev?.map((d) =>
      d.id === update.id
        ? { ...d, isOnline: update.isOnline, lastSeenAt: update.lastSeenAt, report: update.report }
        : d,
    ),
  );
}

/** Applies Vigil's realtime events to the health, incident and Device queries. */
export function useVigilStream() {
  const queryClient = useQueryClient();
  const isLive = useRealtimeStatus() === "live";

  useRealtimeTopic(VIGIL_TOPICS, (message: RealtimeMessage) => {
    if (message.kind === "resync") {
      for (const queryKey of [SERVICE_HEALTH_KEY, SYSTEM_OVERVIEW_KEY, INCIDENTS_KEY, DEVICE_STATUSES_KEY]) {
        void queryClient.invalidateQueries({ queryKey }, { cancelRefetch: false });
      }
      return;
    }

    switch (message.topic) {
      case REALTIME_TOPICS.vigilStatus:
        applyStatus(queryClient, message.data as StatusPayload);
        break;
      case REALTIME_TOPICS.vigilIncidentOpened:
      case REALTIME_TOPICS.vigilIncidentResolved:
        void queryClient.invalidateQueries({ queryKey: INCIDENTS_KEY }, { cancelRefetch: false });
        void queryClient.invalidateQueries({ queryKey: SYSTEM_OVERVIEW_KEY }, { cancelRefetch: false });
        break;
      case REALTIME_TOPICS.vigilDeviceUpdated:
        applyDeviceUpdate(queryClient, message.data as DeviceUpdate);
        break;
      case REALTIME_TOPICS.vigilDeviceRemoved: {
        const removedId = (message.data as { id: string }).id;
        queryClient.setQueryData<DeviceStatus[]>(DEVICE_STATUSES_KEY, (prev) =>
          prev?.filter((d) => d.id !== removedId),
        );
        break;
      }
    }
  });

  return { isLive };
}
