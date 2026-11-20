import { createContext } from "react";

export interface PresenceState {
  isPresent: boolean;
  /** False for a child that was already there when the Presence first rendered with `initial={false}`. */
  animateEnter: boolean;
  /** An exiting child is taken out of the flow so its replacement can take its place at once. */
  popLayout: boolean;
  onExitComplete: () => void;
}

export const PresenceContext = createContext<PresenceState | null>(null);
