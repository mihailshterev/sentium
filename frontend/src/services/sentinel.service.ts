import { client } from "../api/client";
import type { NetworkEvent } from "../types/sentinel";

export const fetchNetworkEvents = (count = 100): Promise<NetworkEvent[]> =>
  client.get<NetworkEvent[]>(`/sentinel/events/network?count=${count}`);
