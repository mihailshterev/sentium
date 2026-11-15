let context: AudioContext | null = null;

export const audioSupported = (): boolean => typeof window !== "undefined" && typeof window.AudioContext === "function";

/** The one AudioContext playback and the microphone meter share. Created on first use. */
export function getAudioContext(): AudioContext | null {
  if (!audioSupported()) {
    return null;
  }
  context ??= new AudioContext();
  return context;
}

/**
 * Browsers keep an AudioContext suspended until a user gesture. Call this from a click or key handler so audio
 * that arrives later (a reply still being generated) is allowed to play.
 */
export function unlockAudio(): void {
  const ctx = getAudioContext();
  if (ctx?.state === "suspended") {
    void ctx.resume().catch(() => {});
  }
}

/** Loudness of what flows through `analyser`, 0 (silence) to 1. */
export function readLevel(analyser: AnalyserNode, scratch: Uint8Array<ArrayBuffer>): number {
  analyser.getByteTimeDomainData(scratch);
  let sum = 0;
  for (const sample of scratch) {
    const centred = (sample - 128) / 128;
    sum += centred * centred;
  }
  // Speech RMS sits well below full scale; scale it so normal talking reaches the upper half.
  return Math.min(1, Math.sqrt(sum / scratch.length) * 4);
}
