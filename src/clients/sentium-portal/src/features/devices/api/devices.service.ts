import { client } from "@/shared/api/client";
import type { Device, DeviceStatus, PairingCode } from "@/features/devices/types/device";

const BASE = "/identity/devices";

export const fetchMyDevices = (): Promise<Device[]> => client.get<Device[]>(BASE);

export const createPairingCode = (): Promise<PairingCode> => client.post<PairingCode>(`${BASE}/pairing-codes`, undefined);

export const removeDevice = (id: string): Promise<void> => client.delete<void>(`${BASE}/${encodeURIComponent(id)}`);

/** The Devices the caller may see with their live state: their own, or everyone's for a Sovereign. */
export const fetchDeviceStatuses = (): Promise<DeviceStatus[]> => client.get<DeviceStatus[]>("/vigil/devices");
