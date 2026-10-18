export interface HarnessSettings {
  userHarnessPrompt: string;
  isBuiltInHarnessEnabled: boolean;
  isPromptEnhancementEnabled: boolean;
}

export interface Settings {
  harness: HarnessSettings;
  updatedAt: string;
  updatedBy: string | null;
}

export interface UpdateSettingsPayload {
  harness: HarnessSettings;
}
