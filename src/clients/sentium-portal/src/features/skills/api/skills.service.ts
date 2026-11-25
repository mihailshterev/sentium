import { BASE_URL, client, handleUnauthorized } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
import type { AgentSkill, AgentSkillType, BuiltInSkill, CreateSkillPayload, UpdateSkillPayload } from "../types/skills";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";
const url = pathIn(BASE);

export const fetchBuiltInSkills = (): Promise<BuiltInSkill[]> => client.get<BuiltInSkill[]>(`${BASE}/skills/built-in`);

export const fetchSkillsPaged = (
  skillType?: AgentSkillType,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<AgentSkill>> =>
  client.get<PagedResponse<AgentSkill>>(withQuery(`${BASE}/skills`, { page, pageSize, skillType }));

export const createSkill = (payload: CreateSkillPayload): Promise<AgentSkill> =>
  client.post<AgentSkill>(`${BASE}/skills`, payload);

export const updateSkill = (id: string, payload: UpdateSkillPayload): Promise<void> =>
  client.put<void>(url`/skills/${id}`, payload);

export const deleteSkill = (id: string): Promise<void> => client.delete<void>(url`/skills/${id}`);

export const uploadSkillFile = async (file: File): Promise<AgentSkill> => {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${BASE_URL}${BASE}/skills/upload`, {
    method: "POST",
    body: formData,
    credentials: "include",
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as { error?: string }).error || `Upload failed: ${response.status}`);
  }

  return response.json() as Promise<AgentSkill>;
};
