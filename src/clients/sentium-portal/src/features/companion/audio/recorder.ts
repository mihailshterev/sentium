import { getAudioContext, readLevel } from "./audio-context";

const MIME_CANDIDATES = ["audio/webm;codecs=opus", "audio/webm", "audio/mp4", "audio/ogg;codecs=opus"];

/** The microphone needs a secure context (HTTPS or localhost) and MediaRecorder. */
export const recordingSupported = (): boolean =>
  typeof window !== "undefined" &&
  window.isSecureContext &&
  typeof window.MediaRecorder === "function" &&
  typeof navigator.mediaDevices?.getUserMedia === "function";

export interface Recording {
  /** Releases the microphone and resolves to the audio, or `null` when nothing was captured. */
  stop: () => Promise<Blob | null>;
  /** Releases the microphone and throws the audio away. */
  cancel: () => void;
  /** Loudness of the microphone input right now, 0 to 1. */
  level: () => number;
  /** The loudest the input has been so far, 0 to 1; `null` when it could not be measured. */
  peak: () => number | null;
}

/** How often the input is sampled for {@link Recording.peak}. */
const PEAK_SAMPLE_MS = 80;

/**
 * Opens the microphone and records until stopped. The stream's tracks are stopped the moment recording ends, so
 * the browser's microphone indicator is on only while push-to-talk is held.
 */
export async function startRecording(): Promise<Recording> {
  const stream = await navigator.mediaDevices.getUserMedia({
    audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1 },
  });

  const mimeType = MIME_CANDIDATES.find((type) => MediaRecorder.isTypeSupported(type));
  const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
  const chunks: Blob[] = [];
  recorder.ondataavailable = (event) => {
    if (event.data.size > 0) {
      chunks.push(event.data);
    }
  };

  const ctx = getAudioContext();
  let meter: MediaStreamAudioSourceNode | null = null;
  let analyser: AnalyserNode | null = null;
  let scratch: Uint8Array<ArrayBuffer> | null = null;
  if (ctx) {
    meter = ctx.createMediaStreamSource(stream);
    analyser = ctx.createAnalyser();
    analyser.fftSize = 512;
    scratch = new Uint8Array(analyser.fftSize);
    // Metering only: the analyser is not connected to the speakers.
    meter.connect(analyser);
  }

  // Sampled independently of the Companion's animation, which does not run under reduced motion.
  let peak: number | null = null;
  const sampler = setInterval(() => {
    if (ctx?.state === "running" && analyser && scratch) {
      peak = Math.max(peak ?? 0, readLevel(analyser, scratch));
    }
  }, PEAK_SAMPLE_MS);

  const release = () => {
    clearInterval(sampler);
    meter?.disconnect();
    stream.getTracks().forEach((track) => track.stop());
  };

  recorder.start();

  return {
    stop: () =>
      new Promise<Blob | null>((resolve) => {
        if (recorder.state === "inactive") {
          release();
          resolve(null);
          return;
        }
        recorder.onstop = () => {
          release();
          resolve(chunks.length > 0 ? new Blob(chunks, { type: recorder.mimeType }) : null);
        };
        recorder.stop();
      }),

    cancel: () => {
      recorder.ondataavailable = null;
      if (recorder.state !== "inactive") {
        recorder.stop();
      }
      release();
    },

    level: () => (analyser && scratch ? readLevel(analyser, scratch) : 0),

    peak: () => peak,
  };
}
