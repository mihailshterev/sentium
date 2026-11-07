import { client } from "@/shared/api/client";
import type { ModelsResponse } from "../types/models";

const BASE = "/praxis";

export const fetchModelCatalog = (): Promise<ModelsResponse> => client.get<ModelsResponse>(`${BASE}/models`);

export const loadModel = (model: string): Promise<void> => client.post<void>(`${BASE}/models/load`, { model });

export const unloadModel = (model: string): Promise<void> => client.post<void>(`${BASE}/models/unload`, { model });
