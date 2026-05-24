import { z } from "zod";

export const agentCreateSchema = z.object({
  name: z.string().min(1, "Agent name is required").max(255),
  description: z.string().max(1000),
  model: z.string().min(1, "Model is required"),
});

export type AgentCreateFormData = z.infer<typeof agentCreateSchema>;
