/** What the Companion is showing. Exactly one at a time; see `companion-machine.ts` for which one wins. */
export type CompanionState = "idle" | "listening" | "thinking" | "speaking" | "alert" | "pleased" | "sleeping";

/** What is really happening right now; the machine turns these into one {@link CompanionState}. */
export interface CompanionSignals {
  /** Push-to-talk is held and the microphone is open. */
  listening: boolean;
  /** A reply or tool call is in progress somewhere. */
  thinking: boolean;
  /** Synthesised audio is playing. */
  speaking: boolean;
  /** An unread Warning or Critical notification exists. */
  alert: boolean;
  /** Something just went well (a task ticked, a run finished). */
  pleased: boolean;
  /** No input for a long while. */
  sleeping: boolean;
}

export interface SpeechVoice {
  id: string;
  name: string;
}

/** `GET /praxis/speech`: which halves of speech the server can do right now. */
export interface SpeechStatus {
  transcription: boolean;
  synthesis: boolean;
  defaultVoice: string;
  voices: SpeechVoice[];
  maxTextLength: number;
}

/** The per-user Registry group `companion`. */
export interface CompanionSettings {
  isCompanionEnabled: boolean;
  isVoiceEnabled: boolean;
  /** `null` uses the server's default Voice. */
  voice: string | null;
  speakingRate: number;
  isReviewBeforeSendingEnabled: boolean;
  isSpeakTypedRepliesEnabled: boolean;
}

export const DEFAULT_COMPANION_SETTINGS: CompanionSettings = {
  isCompanionEnabled: true,
  isVoiceEnabled: true,
  voice: null,
  speakingRate: 1,
  isReviewBeforeSendingEnabled: false,
  isSpeakTypedRepliesEnabled: false,
};

/** Below this width the Companion leaves the sidebar and floats above the bottom edge. */
export const PHONE_QUERY = "(max-width: 640px)";
