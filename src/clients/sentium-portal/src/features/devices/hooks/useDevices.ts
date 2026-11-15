import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createPairingCode,
  fetchDeviceStatuses,
  fetchMyDevices,
  removeDevice,
} from "@/features/devices/api/devices.service";
import type { Device, DeviceStatus } from "@/features/devices/types/device";

export const MY_DEVICES_KEY = ["devices", "mine"] as const;
export const DEVICE_STATUSES_KEY = ["devices", "statuses"] as const;

const STATUS_POLL_INTERVAL = 15_000;

export function useMyDevices() {
  const queryClient = useQueryClient();

  const { data: devices = [], isLoading, error } = useQuery<Device[]>({
    queryKey: MY_DEVICES_KEY,
    queryFn: fetchMyDevices,
  });

  const pairingCode = useMutation({ mutationFn: createPairingCode });

  const remove = useMutation({
    mutationFn: removeDevice,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["devices"] }),
  });

  return {
    devices,
    isLoading,
    error,
    refresh: () => queryClient.invalidateQueries({ queryKey: MY_DEVICES_KEY }),
    pairingCode,
    remove,
  };
}

/** Live Device state for the Vigil page; the Vigil stream patches this cache between polls. */
export function useDeviceStatuses() {
  const { data: devices = [], isLoading } = useQuery<DeviceStatus[]>({
    queryKey: DEVICE_STATUSES_KEY,
    queryFn: fetchDeviceStatuses,
    refetchInterval: STATUS_POLL_INTERVAL,
    retry: false,
  });

  return { devices, isLoading };
}
