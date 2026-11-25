import { useQueryClient, type QueryClient } from "@tanstack/react-query";
import { deviceKeys, type DeviceStatus, type DeviceUpdate } from "@/features/devices";
import { REALTIME_TOPICS, useRealtimeStatus, useRealtimeTopic, type RealtimeMessage } from "@/shared/realtime";
import { vigilKeys } from "../api/vigil.queries";
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
  queryClient.setQueryData<ServiceHealthStatus[]>(vigilKeys.serviceHealth(), (prev) =>
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
  void queryClient.invalidateQueries({ queryKey: vigilKeys.overview() }, { cancelRefetch: false });
}

function applyDeviceUpdate(queryClient: QueryClient, update: DeviceUpdate) {
  const known = queryClient.getQueryData<DeviceStatus[]>(deviceKeys.statuses())?.some((d) => d.id === update.id);
  if (!known) {
    // A Device paired since the list was loaded: the frame has no name or owner, so fetch the list.
    void queryClient.invalidateQueries({ queryKey: deviceKeys.statuses() });
    return;
  }
  queryClient.setQueryData<DeviceStatus[]>(deviceKeys.statuses(), (prev) =>
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
      for (const queryKey of [vigilKeys.serviceHealth(), vigilKeys.overview(), vigilKeys.incidents(), deviceKeys.statuses()]) {
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
        void queryClient.invalidateQueries({ queryKey: vigilKeys.incidents() }, { cancelRefetch: false });
        void queryClient.invalidateQueries({ queryKey: vigilKeys.overview() }, { cancelRefetch: false });
        break;
      case REALTIME_TOPICS.vigilDeviceUpdated:
        applyDeviceUpdate(queryClient, message.data as DeviceUpdate);
        break;
      case REALTIME_TOPICS.vigilDeviceRemoved: {
        const removedId = (message.data as { id: string }).id;
        queryClient.setQueryData<DeviceStatus[]>(deviceKeys.statuses(), (prev) =>
          prev?.filter((d) => d.id !== removedId),
        );
        break;
      }
    }
  });

  return { isLive };
}
