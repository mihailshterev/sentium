import { client } from "@/shared/api/client";
import type { UserMemory } from "../types/user-memory";

const BASE = "/praxis/memories";

export const fetchMemories = (): Promise<UserMemory[]> => client.get<UserMemory[]>(BASE);

/** Content that restates an existing memory replaces it instead of adding a duplicate. */
export const addMemory = (content: string): Promise<UserMemory> => client.post<UserMemory>(BASE, { content });

export const updateMemory = (id: string, content: string): Promise<UserMemory> =>
  client.put<UserMemory>(`${BASE}/${id}`, { content });

export const deleteMemory = (id: string): Promise<void> => client.delete<void>(`${BASE}/${id}`);
