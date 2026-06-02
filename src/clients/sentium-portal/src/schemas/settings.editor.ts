import { z } from "zod";

export const settingsEditorSchema = z.object({
  prompt: z.string().max(16000),
  builtInEnabled: z.boolean(),
  promptEnhancementEnabled: z.boolean(),
});

export type SettingsEditorFormData = z.infer<typeof settingsEditorSchema>;
