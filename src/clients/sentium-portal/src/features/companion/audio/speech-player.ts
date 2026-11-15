import { synthesize, type SpeakOptions } from "../api/speech.service";
import { getAudioContext, readLevel } from "./audio-context";

/** Sentences synthesised ahead of the one playing. More would only queue up on the CPU-bound speech server. */
const MAX_IN_FLIGHT = 2;

interface QueueItem {
  text: string;
  options: SpeakOptions;
  audio: Promise<AudioBuffer | null> | null;
}

export interface PlayerStatus {
  /** Audio is playing. */
  speaking: boolean;
  /** Sentences are queued but the first audio has not arrived yet. */
  preparing: boolean;
}

type Listener = (status: PlayerStatus) => void;

/**
 * Plays synthesised sentences in order through one Web Audio chain. Sentences are requested as they are queued,
 * so speech starts while the answer is still streaming, and {@link stop} silences everything at once.
 */
function createSpeechPlayer() {
  const listeners = new Set<Listener>();
  let queue: QueueItem[] = [];
  let source: AudioBufferSourceNode | null = null;
  let analyser: AnalyserNode | null = null;
  let scratch: Uint8Array<ArrayBuffer> | null = null;
  let abort = new AbortController();
  let speaking = false;
  let preparing = false;
  let pumping = false;

  const setStatus = (nextSpeaking: boolean, nextPreparing: boolean) => {
    if (speaking !== nextSpeaking || preparing !== nextPreparing) {
      speaking = nextSpeaking;
      preparing = nextPreparing;
      listeners.forEach((listener) => listener({ speaking, preparing }));
    }
  };
  const setSpeaking = (next: boolean) => setStatus(next, false);

  const load = async (item: QueueItem, signal: AbortSignal): Promise<AudioBuffer | null> => {
    const ctx = getAudioContext();
    if (!ctx) {
      return null;
    }
    try {
      const bytes = await synthesize(item.text, item.options, signal);
      return bytes ? await ctx.decodeAudioData(bytes) : null;
    } catch {
      return null;
    }
  };

  const prefetch = () => {
    for (const item of queue.slice(0, MAX_IN_FLIGHT)) {
      item.audio ??= load(item, abort.signal);
    }
  };

  const play = (ctx: AudioContext, buffer: AudioBuffer, signal: AbortSignal) =>
    new Promise<void>((resolve) => {
      if (!analyser) {
        analyser = ctx.createAnalyser();
        analyser.fftSize = 512;
        analyser.connect(ctx.destination);
        scratch = new Uint8Array(analyser.fftSize);
      }

      const node = ctx.createBufferSource();
      node.buffer = buffer;
      node.connect(analyser);
      node.onended = () => {
        if (source === node) {
          source = null;
        }
        resolve();
      };
      signal.addEventListener("abort", () => resolve(), { once: true });

      source = node;
      node.start();
      setSpeaking(true);
    });

  const pump = async () => {
    if (pumping) {
      return;
    }
    pumping = true;
    const signal = abort.signal;

    while (queue.length > 0 && !signal.aborted) {
      prefetch();
      const item = queue[0];
      const buffer = await item.audio;
      if (signal.aborted) {
        break;
      }

      queue.shift();
      const ctx = getAudioContext();
      // A sentence that could not be synthesised is skipped; a suspended context means no gesture unlocked audio.
      if (buffer && ctx && ctx.state === "running") {
        await play(ctx, buffer, signal);
      }
    }

    pumping = false;
    if (!signal.aborted) {
      setSpeaking(false);
    } else if (queue.length > 0) {
      // stop() ran and new sentences were queued before this loop noticed.
      void pump();
    }
  };

  return {
    enqueue(text: string, options: SpeakOptions) {
      queue.push({ text, options, audio: null });
      if (!speaking) {
        setStatus(false, true);
      }
      prefetch();
      void pump();
    },

    /** Silences playback and drops everything queued or still being synthesised. */
    stop() {
      abort.abort();
      abort = new AbortController();
      queue = [];
      try {
        source?.stop();
      } catch {
        // Already ended.
      }
      source = null;
      setSpeaking(false);
    },

    isSpeaking: () => speaking,

    /** Loudness of the audio playing right now, 0 to 1. */
    level: () => (speaking && analyser && scratch ? readLevel(analyser, scratch) : 0),

    subscribe(listener: Listener) {
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
  };
}

export const speechPlayer = createSpeechPlayer();
