import { z } from "zod";

export const settingsEditorSchema = z.object({
  UserHarnessPrompt: z.string().max(16000),
  IsBuiltInHarnessEnabled: z.boolean(),
  IsPromptEnhancementEnabled: z.boolean(),

  defaultModel: z.string(),
  agentTemperature: z.number().min(0).max(1),
  agentContextWindow: z.number().min(512).max(131072),
});

export type SettingsEditorFormData = z.infer<typeof settingsEditorSchema>;
