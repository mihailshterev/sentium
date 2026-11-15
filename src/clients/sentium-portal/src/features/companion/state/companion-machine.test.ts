import { describe, expect, it } from "vitest";
import type { CompanionSignals } from "../types/companion";
import { SETTLE_MS, advance, describeState, initialMachine, resolveState, settleDelay } from "./companion-machine";

const signals = (active: Partial<CompanionSignals> = {}): CompanionSignals => ({
  listening: false,
  thinking: false,
  speaking: false,
  alert: false,
  pleased: false,
  sleeping: false,
  ...active,
});

describe("resolveState", () => {
  it("is idle when nothing is happening", () => {
    expect(resolveState(signals())).toBe("idle");
  });

  it("ranks listening over speaking over thinking over alert over pleased over sleeping", () => {
    const all = signals({ listening: true, speaking: true, thinking: true, alert: true, pleased: true, sleeping: true });
    expect(resolveState(all)).toBe("listening");
    expect(resolveState({ ...all, listening: false })).toBe("speaking");
    expect(resolveState({ ...all, listening: false, speaking: false })).toBe("thinking");
    expect(resolveState(signals({ alert: true, pleased: true, sleeping: true }))).toBe("alert");
    expect(resolveState(signals({ pleased: true, sleeping: true }))).toBe("pleased");
    expect(resolveState(signals({ sleeping: true }))).toBe("sleeping");
  });
});

describe("advance", () => {
  it("shows listening and speaking at once", () => {
    expect(advance(initialMachine, "listening", 0).shown).toBe("listening");
    expect(advance(initialMachine, "speaking", 0).shown).toBe("speaking");
  });

  it("holds other states back until they have lasted the settle delay", () => {
    const wanted = advance(initialMachine, "thinking", 1000);
    expect(wanted.shown).toBe("idle");
    expect(settleDelay(wanted, 1000)).toBe(SETTLE_MS);

    expect(advance(wanted, "thinking", 1000 + SETTLE_MS - 1).shown).toBe("idle");
    expect(advance(wanted, "thinking", 1000 + SETTLE_MS).shown).toBe("thinking");
  });

  it("skips a state that ends before it settled", () => {
    const wanted = advance(initialMachine, "thinking", 0);
    const back = advance(wanted, "idle", 100);

    expect(back).toEqual({ shown: "idle", pending: null });
    expect(settleDelay(back, 100)).toBeNull();
  });

  it("restarts the delay when the wanted state changes while waiting", () => {
    const thinking = advance(initialMachine, "thinking", 0);
    const alert = advance(thinking, "alert", 200);

    expect(alert.pending).toEqual({ state: "alert", since: 200 });
    expect(advance(alert, "alert", SETTLE_MS).shown).toBe("idle");
    expect(advance(alert, "alert", 200 + SETTLE_MS).shown).toBe("alert");
  });

  it("returns the same machine when nothing changes", () => {
    expect(advance(initialMachine, "idle", 50)).toBe(initialMachine);
  });
});

describe("describeState", () => {
  it("gives every state a text equivalent", () => {
    expect(describeState("alert")).toBe("something needs your attention");
    expect(describeState("listening")).toBe("listening");
  });
});
