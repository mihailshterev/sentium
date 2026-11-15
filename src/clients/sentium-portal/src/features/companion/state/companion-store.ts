import { create } from "zustand";
import type { CompanionSignals, CompanionState } from "../types/companion";
import { advance, initialMachine, resolveState, settleDelay, type CompanionMachine } from "./companion-machine";

/** How long the Companion stays pleased after something went well. */
const PLEASED_MS = 1600;

/** What voice may do right now: the user's settings combined with what the server and browser can do. */
export interface VoicePolicy {
  canListen: boolean;
  canSpeak: boolean;
  voice: string | null;
  rate: number;
  reviewBeforeSending: boolean;
  speakTypedReplies: boolean;
}

export type VoicePhase = "idle" | "recording" | "transcribing";

interface CompanionStoreState {
  signals: CompanionSignals;
  state: CompanionState;
  voice: VoicePolicy;
  voicePhase: VoicePhase;
  voiceError: string | null;

  setSignal: (signal: keyof CompanionSignals, active: boolean) => void;
  /** Something went well: shows the pleased state briefly. */
  cheer: () => void;
  setVoice: (voice: VoicePolicy) => void;
  setVoicePhase: (phase: VoicePhase, error?: string | null) => void;
}

const NO_SIGNALS: CompanionSignals = {
  listening: false,
  thinking: false,
  speaking: false,
  alert: false,
  pleased: false,
  sleeping: false,
};

const NO_VOICE: VoicePolicy = {
  canListen: false,
  canSpeak: false,
  voice: null,
  rate: 1,
  reviewBeforeSending: false,
  speakTypedReplies: false,
};

let machine: CompanionMachine = initialMachine;
let settleTimer: ReturnType<typeof setTimeout> | undefined;
let pleasedTimer: ReturnType<typeof setTimeout> | undefined;

/** Module scope, like the conversation stores: the Companion keeps its state across navigation. */
export const useCompanionStore = create<CompanionStoreState>((set, get) => {
  const step = () => {
    clearTimeout(settleTimer);
    const now = Date.now();
    machine = advance(machine, resolveState(get().signals), now);

    if (machine.shown !== get().state) {
      set({ state: machine.shown });
    }

    const delay = settleDelay(machine, now);
    if (delay !== null) {
      settleTimer = setTimeout(step, delay);
    }
  };

  return {
    signals: NO_SIGNALS,
    state: initialMachine.shown,
    voice: NO_VOICE,
    voicePhase: "idle",
    voiceError: null,

    setSignal: (signal, active) => {
      if (get().signals[signal] === active) {
        return;
      }
      set((s) => ({ signals: { ...s.signals, [signal]: active } }));
      step();
    },

    cheer: () => {
      clearTimeout(pleasedTimer);
      get().setSignal("pleased", true);
      pleasedTimer = setTimeout(() => get().setSignal("pleased", false), PLEASED_MS);
    },

    setVoice: (voice) => set({ voice }),

    setVoicePhase: (voicePhase, voiceError = null) => set({ voicePhase, voiceError }),
  };
});

/** What to tell the user about push-to-talk right now: progress, the last problem, or nothing. */
export function useVoiceHint(): string | null {
  const phase = useCompanionStore((s) => s.voicePhase);
  const error = useCompanionStore((s) => s.voiceError);

  if (phase === "recording") {
    return "Listening…";
  }
  return phase === "transcribing" ? "Transcribing…" : error;
}

/** Test hook: back to a fresh Companion. */
export function resetCompanionStore(): void {
  clearTimeout(settleTimer);
  clearTimeout(pleasedTimer);
  machine = initialMachine;
  useCompanionStore.setState({
    signals: NO_SIGNALS,
    state: initialMachine.shown,
    voice: NO_VOICE,
    voicePhase: "idle",
    voiceError: null,
  });
}
