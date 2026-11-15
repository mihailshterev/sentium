import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SETTLE_MS } from "./companion-machine";
import { resetCompanionStore, useCompanionStore } from "./companion-store";

const store = () => useCompanionStore.getState();

beforeEach(() => {
  vi.useFakeTimers();
  resetCompanionStore();
});

afterEach(() => {
  resetCompanionStore();
  vi.useRealTimers();
});

describe("companion store", () => {
  it("starts idle", () => {
    expect(store().state).toBe("idle");
  });

  it("shows thinking once a reply has been in progress for the settle delay", () => {
    store().setSignal("thinking", true);
    expect(store().state).toBe("idle");

    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("thinking");
  });

  it("never shows a reply that finished within the settle delay", () => {
    store().setSignal("thinking", true);
    vi.advanceTimersByTime(SETTLE_MS - 50);
    store().setSignal("thinking", false);
    vi.advanceTimersByTime(SETTLE_MS * 2);

    expect(store().state).toBe("idle");
  });

  it("moves through listening, thinking and speaking for a spoken turn", () => {
    store().setSignal("listening", true);
    expect(store().state).toBe("listening");

    store().setSignal("listening", false);
    store().setSignal("thinking", true);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("thinking");

    store().setSignal("speaking", true);
    expect(store().state).toBe("speaking");

    store().setSignal("thinking", false);
    store().setSignal("speaking", false);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("idle");
  });

  it("stays alert until the notification is acknowledged, but lets speech show through", () => {
    store().setSignal("alert", true);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("alert");

    store().setSignal("speaking", true);
    expect(store().state).toBe("speaking");

    store().setSignal("speaking", false);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("alert");

    store().setSignal("alert", false);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("idle");
  });

  it("is pleased briefly after a cheer", () => {
    store().cheer();
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("pleased");

    vi.advanceTimersByTime(2000);
    expect(store().state).toBe("idle");
  });

  it("wakes from sleep when the signal clears", () => {
    store().setSignal("sleeping", true);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("sleeping");

    store().setSignal("sleeping", false);
    vi.advanceTimersByTime(SETTLE_MS);
    expect(store().state).toBe("idle");
  });

  it("records the voice phase and its error", () => {
    store().setVoicePhase("idle", "I didn't catch that.");
    expect(store().voiceError).toBe("I didn't catch that.");

    store().setVoicePhase("recording");
    expect(store()).toMatchObject({ voicePhase: "recording", voiceError: null });
  });
});
