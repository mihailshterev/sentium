import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as speechService from "../api/speech.service";
import { resetCompanionStore, useCompanionStore } from "../state/companion-store";
import { beginPushToTalk, endPushToTalk } from "./push-to-talk";
import * as recorder from "./recorder";
import { speechPlayer } from "./speech-player";

const store = () => useCompanionStore.getState();

const recording = (peak: number | null, audio: Blob | null = new Blob(["audio"], { type: "audio/webm" })) => ({
  stop: vi.fn().mockResolvedValue(audio),
  cancel: vi.fn(),
  level: () => 0,
  peak: () => peak,
});

/** Holds push-to-talk for `ms`, long enough to count as speech unless stated otherwise. */
const hold = async (ms = 1000) => {
  beginPushToTalk();
  await vi.advanceTimersByTimeAsync(ms);
  return endPushToTalk();
};

describe("push-to-talk", () => {
  let transcribe: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.useFakeTimers();
    resetCompanionStore();
    vi.spyOn(speechPlayer, "stop").mockImplementation(() => {});
    transcribe = vi.spyOn(speechService, "transcribe").mockResolvedValue("where is the drill?");
  });

  afterEach(() => {
    resetCompanionStore();
    vi.useRealTimers();
  });

  it("listens while held and returns the transcript on release", async () => {
    vi.spyOn(recorder, "startRecording").mockResolvedValue(recording(0.4));

    beginPushToTalk();
    await vi.advanceTimersByTimeAsync(1000);
    expect(store().signals.listening).toBe(true);
    expect(speechPlayer.stop).toHaveBeenCalled();

    await expect(endPushToTalk()).resolves.toBe("where is the drill?");
    expect(store()).toMatchObject({ voicePhase: "idle", voiceError: null });
    expect(store().signals.listening).toBe(false);
  });

  it("never uploads a recording in which nothing was heard, and says why", async () => {
    vi.spyOn(recorder, "startRecording").mockResolvedValue(recording(0.001));

    await expect(hold()).resolves.toBeNull();

    expect(transcribe).not.toHaveBeenCalled();
    expect(store().voiceError).toMatch(/couldn't hear anything/i);
  });

  it("still transcribes when the input level could not be measured", async () => {
    vi.spyOn(recorder, "startRecording").mockResolvedValue(recording(null));

    await expect(hold()).resolves.toBe("where is the drill?");
  });

  it("sends nothing when the server heard no speech", async () => {
    vi.spyOn(recorder, "startRecording").mockResolvedValue(recording(0.4));
    transcribe.mockResolvedValue("  ");

    await expect(hold()).resolves.toBeNull();
    expect(store().voiceError).toBe("I didn't catch that.");
  });

  it("discards a slip of the finger without transcribing", async () => {
    const slip = recording(0.4);
    vi.spyOn(recorder, "startRecording").mockResolvedValue(slip);

    await expect(hold(100)).resolves.toBeNull();

    expect(slip.cancel).toHaveBeenCalled();
    expect(transcribe).not.toHaveBeenCalled();
    expect(store().voiceError).toBeNull();
  });

  it("reports a blocked microphone", async () => {
    vi.spyOn(recorder, "startRecording").mockRejectedValue(new DOMException("denied", "NotAllowedError"));

    await expect(hold()).resolves.toBeNull();
    expect(store()).toMatchObject({ voicePhase: "idle", voiceError: "Microphone access is blocked for this site." });
  });

  it("reports a failed transcription", async () => {
    vi.spyOn(recorder, "startRecording").mockResolvedValue(recording(0.4));
    transcribe.mockRejectedValue(new Error("503"));

    await expect(hold()).resolves.toBeNull();
    expect(store().voiceError).toBe("The recording could not be transcribed.");
  });
});
