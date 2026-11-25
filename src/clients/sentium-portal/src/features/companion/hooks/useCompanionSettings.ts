import { useQuery } from "@tanstack/react-query";
import { settingsQueries } from "@/features/registry/api/registry.queries";
import { speechQueries } from "../api/speech.queries";
import { DEFAULT_COMPANION_SETTINGS, type CompanionSettings, type SpeechStatus } from "../types/companion";

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
    ...settingsQueries.group<Partial<CompanionSettings>>("companion"),
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
  const { data } = useQuery({ ...speechQueries.status(), enabled, retry: false, refetchInterval: 60_000 });

  return data ?? NO_SPEECH;
}
