import { useQuery } from "@tanstack/react-query";
import { fetchSettingsGroup } from "@/features/registry/api/registry.service";
import { settingsQueryKey } from "@/features/registry/hooks/useSettingsGroups";
import { fetchSpeechStatus } from "../api/speech.service";
import { DEFAULT_COMPANION_SETTINGS, type CompanionSettings, type SpeechStatus } from "../types/companion";

export const SPEECH_STATUS_QUERY_KEY = ["speech-status"] as const;

const NO_SPEECH: SpeechStatus = {
  transcription: false,
  synthesis: false,
  defaultVoice: "",
  voices: [],
  maxTextLength: 0,
};

/** The caller's Companion settings. Shares the Settings page's query, so a save there applies at once. */
export function useCompanionSettings(): CompanionSettings {
  const { data } = useQuery({
    queryKey: settingsQueryKey("companion"),
    queryFn: () => fetchSettingsGroup<Partial<CompanionSettings>>("companion"),
    retry: false,
    staleTime: 60_000,
  });

  return { ...DEFAULT_COMPANION_SETTINGS, ...data?.value };
}

/**
 * Which halves of speech the server can do right now. Polled, so voice controls appear and disappear with the
 * speech containers; a failed request simply reads as "no speech".
 */
export function useSpeechStatus(enabled = true): SpeechStatus {
  const { data } = useQuery({
    queryKey: SPEECH_STATUS_QUERY_KEY,
    queryFn: fetchSpeechStatus,
    enabled,
    retry: false,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  return data ?? NO_SPEECH;
}
