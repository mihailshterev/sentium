import { sleep } from "k6";
import { NO_THINK } from "./config.js";
import { isArrivalRate } from "../profiles.js";

/** A user's pause between actions; arrival-rate profiles pace iterations themselves, so they never think. */
export function think(minSeconds, maxSeconds) {
  if (NO_THINK || isArrivalRate) {
    return;
  }
  sleep(minSeconds + Math.random() * (maxSeconds - minSeconds));
}

export function pick(items) {
  return items[Math.floor(Math.random() * items.length)];
}
