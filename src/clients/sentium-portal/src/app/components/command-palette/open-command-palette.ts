export const OPEN_COMMAND_PALETTE_EVENT = "sentium:open-command-palette";

/** Opens the command palette from anywhere, e.g. a search button. */
export const openCommandPalette = () => window.dispatchEvent(new Event(OPEN_COMMAND_PALETTE_EVENT));

const isApple = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.userAgent);

export const COMMAND_PALETTE_SHORTCUT = isApple ? "⌘K" : "Ctrl K";
