import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createPairingCode, removeDevice } from "@/features/devices/api/devices.service";
import { deviceKeys, deviceQueries } from "@/features/devices/api/devices.queries";

/** The Vigil stream patches Device state as it changes; the poll only covers a missed event. */
const STATUS_POLL_INTERVAL = 60_000;

export function useMyDevices() {
  const queryClient = useQueryClient();

  const { data: devices = [], isLoading, error } = useQuery(deviceQueries.mine());

  const pairingCode = useMutation({ mutationFn: createPairingCode });

  const remove = useMutation({
    mutationFn: removeDevice,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: deviceKeys.all }),
  });

  return {
    devices,
    isLoading,
    error,
    refresh: () => queryClient.invalidateQueries({ queryKey: deviceKeys.mine() }),
    pairingCode,
    remove,
  };
}

/** Live Device state for the Vigil page; the Vigil stream patches this cache between polls. */
export function useDeviceStatuses() {
  const { data: devices = [], isLoading } = useQuery({
    ...deviceQueries.statuses(),
    refetchInterval: STATUS_POLL_INTERVAL,
    retry: false,
  });

  return { devices, isLoading };
}
