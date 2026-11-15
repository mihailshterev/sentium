export const CODE_OMITTED = "Code omitted.";
const LINK_WORD = "link";

/** Pieces shorter than this wait for the next one, so list numbers and stray fragments are not spoken alone. */
const MIN_PIECE_LENGTH = 24;
/** Well under the server's limit for one synthesis request. */
const MAX_PIECE_LENGTH = 400;
const FENCE = "```";

/**
 * Turns one piece of markdown into what should be said aloud: formatting marks dropped, link and image text kept,
 * bare URLs replaced by the word "link". Returns an empty string when nothing speakable is left.
 */
export function toSpeakable(markdown: string): string {
  const text = markdown
    .replace(/<[^>]+>/g, " ")
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/\bhttps?:\/\/\S+/gi, LINK_WORD)
    .replace(/^\s{0,3}(#{1,6}|>+)\s*/gm, "")
    .replace(/^\s*([-*+]|\d+[.)])\s+/gm, "")
    .replace(/^\s*[-=*_]{3,}\s*$/gm, " ")
    .replace(/\|/g, " ")
    .replace(/[*_~`]+/g, "")
    .replace(/\s+/g, " ")
    .trim();

  return /[\p{L}\p{N}]/u.test(text) ? text : "";
}

/** Index just past the first sentence or line end in `text`, or -1 when it holds no complete sentence yet. */
function sentenceEnd(text: string): number {
  const match = /[.!?…]["')\]]*\s|\n/.exec(text);
  return match ? match.index + match[0].length : -1;
}

function splitLong(piece: string): string[] {
  const parts: string[] = [];
  let rest = piece;
  while (rest.length > MAX_PIECE_LENGTH) {
    const window = rest.slice(0, MAX_PIECE_LENGTH);
    const cut = Math.max(window.lastIndexOf(", "), window.lastIndexOf("; "), window.lastIndexOf(" "));
    const at = cut > MAX_PIECE_LENGTH / 2 ? cut + 1 : MAX_PIECE_LENGTH;
    parts.push(rest.slice(0, at).trim());
    rest = rest.slice(at);
  }
  if (rest.trim()) {
    parts.push(rest.trim());
  }
  return parts;
}

export interface SpeechChunker {
  /** Feeds more of the streaming answer; returns the pieces that are now complete and ready to speak. */
  push: (delta: string) => string[];
  /** The answer has ended: returns whatever is left. */
  flush: () => string[];
}

/**
 * Splits a streaming markdown answer into speakable sentences as it arrives. Fenced code blocks are never read:
 * each one becomes a single "Code omitted."
 */
export function createSpeechChunker(): SpeechChunker {
  let pending = "";
  let inFence = false;
  let held = "";

  const emit = (out: string[], raw: string, force: boolean) => {
    const speakable = toSpeakable(raw);
    if (speakable) {
      held = held ? `${held} ${speakable}` : speakable;
    }
    if (held && (force || held.length >= MIN_PIECE_LENGTH)) {
      out.push(...splitLong(held));
      held = "";
    }
  };

  const drain = (final: boolean): string[] => {
    const out: string[] = [];

    for (;;) {
      if (inFence) {
        const close = pending.indexOf(FENCE);
        if (close === -1) {
          // Keep a possible half-received closing fence; the code itself is dropped.
          pending = final ? "" : pending.slice(-(FENCE.length - 1));
          break;
        }
        pending = pending.slice(close + FENCE.length);
        inFence = false;
        continue;
      }

      const fence = pending.indexOf(FENCE);
      const end = sentenceEnd(pending);

      if (fence !== -1 && (end === -1 || fence < end)) {
        emit(out, pending.slice(0, fence), true);
        out.push(CODE_OMITTED);
        pending = pending.slice(fence + FENCE.length);
        inFence = true;
        continue;
      }

      if (end === -1) {
        break;
      }

      emit(out, pending.slice(0, end), false);
      pending = pending.slice(end);
    }

    if (final) {
      emit(out, inFence ? "" : pending, true);
      pending = "";
    }

    return out;
  };

  return {
    push: (delta) => {
      pending += delta;
      return drain(false);
    },
    flush: () => drain(true),
  };
}
