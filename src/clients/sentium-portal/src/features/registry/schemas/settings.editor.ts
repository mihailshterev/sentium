import { z } from "zod";

export const settingsEditorSchema = z.object({
  userHarnessPrompt: z.string().max(16000),
  isBuiltInHarnessEnabled: z.boolean(),
  isPromptEnhancementEnabled: z.boolean(),

  defaultModel: z.string(),
  agentTemperature: z.number().min(0).max(1),

  /** Empty falls back to the household default. Times are `HH:mm` (time inputs). */
  timeZone: z.string(),
  dayStart: z.string(),
  householdTimeZone: z.string(),
  householdDayStart: z.string(),
});

export type SettingsEditorFormData = z.infer<typeof settingsEditorSchema>;
