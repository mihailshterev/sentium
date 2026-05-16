import { client, BASE_URL } from "../api/client";
import type { SandboxExecutionLog } from "../types/sandbox";

const BASE = "/sandbox";

export const fetchExecutionLogs = (count = 100): Promise<SandboxExecutionLog[]> =>
  client.get<SandboxExecutionLog[]>(`${BASE}/executions?count=${count}`);

export const getArtifactUrl = (downloadPath: string): string => `${BASE_URL}${BASE}/artifacts/${downloadPath}`;
