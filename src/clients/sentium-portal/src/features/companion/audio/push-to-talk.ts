import { transcribe } from "../api/speech.service";
import { useCompanionStore } from "../state/companion-store";
import { unlockAudio } from "./audio-context";
import { startRecording, type Recording } from "./recorder";
import { speechPlayer } from "./speech-player";

/** A press shorter than this is a slip, not speech. */
const MIN_HOLD_MS = 350;
/** A recording that never got louder than this holds no sound at all: a muted or wrong microphone. */
const SILENT_PEAK = 0.01;

const NOTHING_HEARD = "I couldn't hear anything. Check that the right microphone is selected and not muted.";

let pending: Promise<Recording | null> | null = null;
let active: Recording | null = null;
let startedAt = 0;

const store = () => useCompanionStore.getState();

const microphoneError = (err: unknown): string =>
  err instanceof DOMException && (err.name === "NotAllowedError" || err.name === "SecurityError")
    ? "Microphone access is blocked for this site."
    : "No microphone is available.";

/**
 * Push-to-talk held: silences any speech and opens the microphone. One recording at a time, shared by every
 * push-to-talk control, so the Companion's listening state always matches the open microphone.
 */
export function beginPushToTalk(): void {
  if (pending || store().voicePhase !== "idle") {
    return;
  }

  speechPlayer.stop();
  unlockAudio();
  startedAt = Date.now();
  store().setVoicePhase("recording");

  pending = startRecording()
    .then((recording) => {
      active = recording;
      store().setSignal("listening", true);
      return recording;
    })
    .catch((err: unknown) => {
      store().setVoicePhase("idle", microphoneError(err));
      return null;
    });
}

/**
 * Push-to-talk released: closes the microphone and resolves to the transcript, or `null` when there is nothing to
 * send (a slip, silence, or a failure, which is reported through the store's `voiceError`).
 */
export async function endPushToTalk(): Promise<string | null> {
  const starting = pending;
  if (!starting) {
    return null;
  }

  // Cleared before awaiting, so a second release (pointer up, then blur) finds nothing to end.
  pending = null;

  // Release can beat the permission prompt; wait for the microphone so it is always closed again.
  const recording = await starting;
  active = null;
  store().setSignal("listening", false);
  if (!recording) {
    return null;
  }

  if (Date.now() - startedAt < MIN_HOLD_MS) {
    recording.cancel();
    store().setVoicePhase("idle");
    return null;
  }

  const peak = recording.peak();
  const audio = await recording.stop();
  if (!audio) {
    store().setVoicePhase("idle");
    return null;
  }

  // Nothing to transcribe, and a silent clip is never uploaded.
  if (peak !== null && peak < SILENT_PEAK) {
    store().setVoicePhase("idle", NOTHING_HEARD);
    return null;
  }

  store().setVoicePhase("transcribing");
  try {
    const text = (await transcribe(audio)).trim();
    store().setVoicePhase("idle", text ? null : "I didn't catch that.");
    return text || null;
  } catch {
    store().setVoicePhase("idle", "The recording could not be transcribed.");
    return null;
  }
}

/** Loudness of the open microphone, 0 to 1. */
export const microphoneLevel = (): number => active?.level() ?? 0;
