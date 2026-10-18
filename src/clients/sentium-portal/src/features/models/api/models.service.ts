import { BASE_URL, client } from "@/shared/api/client";
import type { DeleteModelResult, OllamaModel } from "../types/models";

const BASE = "/agent-runtime";

export const fetchModels = async (): Promise<string[]> => {
  const models = await client.get<OllamaModel[]>(`${BASE}/models`);
  return models.map((model) => model.name);
};

export const fetchOllamaModels = () => client.get<OllamaModel[]>(`${BASE}/models`);

export const pullModel = (name: string, signal?: AbortSignal): Promise<Response> =>
  fetch(`${BASE_URL}${BASE}/models/pull`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
    credentials: "include",
    signal,
  });

export const deleteOllamaModel = (name: string): Promise<DeleteModelResult> =>
  client.delete<DeleteModelResult>(`${BASE}/models?name=${encodeURIComponent(name)}`);
