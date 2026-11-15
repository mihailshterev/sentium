import type { CompanionSignals, CompanionState } from "../types/companion";

/**
 * Highest first. Listening outranks everything because the user is holding a button and must see it at once;
 * after that the assistant's own activity (speaking, thinking) outranks ambient states.
 */
const PRIORITY: { state: CompanionState; signal: keyof CompanionSignals }[] = [
  { state: "listening", signal: "listening" },
  { state: "speaking", signal: "speaking" },
  { state: "thinking", signal: "thinking" },
  { state: "alert", signal: "alert" },
  { state: "pleased", signal: "pleased" },
  { state: "sleeping", signal: "sleeping" },
];

/** A state must have been wanted for this long before it is shown, so one that lasts under a moment never flickers. */
export const SETTLE_MS = 280;

/** States tied to something the user is doing or hearing right now are shown without the settle delay. */
const IMMEDIATE: ReadonlySet<CompanionState> = new Set<CompanionState>(["listening", "speaking"]);

export function resolveState(signals: CompanionSignals): CompanionState {
  return PRIORITY.find(({ signal }) => signals[signal])?.state ?? "idle";
}

export interface CompanionMachine {
  shown: CompanionState;
  /** The state waiting out its settle delay, with the time it was first wanted. */
  pending: { state: CompanionState; since: number } | null;
}

export const initialMachine: CompanionMachine = { shown: "idle", pending: null };

/**
 * Moves the machine toward the state the signals want. Call it whenever the signals change and again once
 * {@link settleDelay} has passed.
 */
export function advance(machine: CompanionMachine, wanted: CompanionState, now: number): CompanionMachine {
  if (wanted === machine.shown) {
    return machine.pending ? { shown: machine.shown, pending: null } : machine;
  }

  if (IMMEDIATE.has(wanted)) {
    return { shown: wanted, pending: null };
  }

  if (machine.pending?.state !== wanted) {
    return { shown: machine.shown, pending: { state: wanted, since: now } };
  }

  return now - machine.pending.since >= SETTLE_MS ? { shown: wanted, pending: null } : machine;
}

/** Milliseconds until the pending state settles, or `null` when nothing is pending. */
export function settleDelay(machine: CompanionMachine, now: number): number | null {
  return machine.pending ? Math.max(0, machine.pending.since + SETTLE_MS - now) : null;
}

const DESCRIPTIONS: Record<CompanionState, string> = {
  idle: "idle",
  listening: "listening",
  thinking: "thinking",
  speaking: "speaking",
  alert: "something needs your attention",
  pleased: "pleased",
  sleeping: "sleeping",
};

/** The text equivalent of a state, for screen readers. */
export const describeState = (state: CompanionState): string => DESCRIPTIONS[state];
