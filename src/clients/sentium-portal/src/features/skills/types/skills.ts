export type AgentSkillType = 0 | 1; // 0 = Custom, 1 = Uploaded

export interface AgentSkill {
  id: string;
  name: string;
  description: string;
  instructions: string;
  skillType: AgentSkillType;
  fileName: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface BuiltInSkill {
  name: string;
  description: string;
  instructions: string;
}

export interface CreateSkillPayload {
  name: string;
  description: string;
  instructions: string;
  skillType: AgentSkillType;
  fileName?: string | null;
}

export interface UpdateSkillPayload {
  name: string;
  description: string;
  instructions: string;
}
