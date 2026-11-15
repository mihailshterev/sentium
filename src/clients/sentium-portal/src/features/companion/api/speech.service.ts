import { BASE_URL, client, handleUnauthorized } from "@/shared/api/client";
import type { SpeechStatus } from "../types/companion";

const BASE = "/praxis/speech";

export const fetchSpeechStatus = () => client.get<SpeechStatus>(BASE);

const EXTENSIONS: [string, string][] = [
  ["webm", "webm"],
  ["mp4", "mp4"],
  ["ogg", "ogg"],
  ["wav", "wav"],
];

const fileNameFor = (mimeType: string) =>
  `speech.${EXTENSIONS.find(([marker]) => mimeType.includes(marker))?.[1] ?? "webm"}`;

/** Sends one push-to-talk recording and returns its transcript (empty when nothing was said). */
export async function transcribe(recording: Blob): Promise<string> {
  const form = new FormData();
  form.append("file", recording, fileNameFor(recording.type));

  const { text } = await client.postForm<{ text: string }>(`${BASE}/transcribe`, form);
  return text ?? "";
}

export interface SpeakOptions {
  voice: string | null;
  speed: number;
}

/** Synthesises one sentence. Resolves to `null` when speech is unavailable or refused, which is never an error to show. */
export async function synthesize(
  text: string,
  { voice, speed }: SpeakOptions,
  signal: AbortSignal,
): Promise<ArrayBuffer | null> {
  const response = await fetch(`${BASE_URL}${BASE}/speak`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ text, voice, speed }),
    credentials: "include",
    signal,
  });

  if (response.status === 401) {
    handleUnauthorized();
  }

  return response.ok ? response.arrayBuffer() : null;
}
