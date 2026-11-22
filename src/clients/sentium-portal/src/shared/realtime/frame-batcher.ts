/** Long enough to gather a burst into one render, short enough that streamed text still feels live. */
const DEFAULT_BATCH_MS = 50;

export interface FrameBatcher<T> {
  push: (frame: T) => void;
  /** Applies whatever is waiting now, e.g. before handling a terminal frame. */
  flush: () => void;
  /** Drops whatever is waiting. */
  cancel: () => void;
}

/**
 * Collects stream frames and hands them over in batches. An EventSource dispatches every event as its own task, so
 * applying each one to a store renders once per token - and a replay after a reload delivers thousands at once, each
 * re-rendering an ever-growing answer.
 */
export function createFrameBatcher<T>(apply: (frames: T[]) => void, delayMs = DEFAULT_BATCH_MS): FrameBatcher<T> {
  let pending: T[] = [];
  let timer: ReturnType<typeof setTimeout> | null = null;

  const flush = () => {
    if (timer) {
      clearTimeout(timer);
      timer = null;
    }
    if (pending.length === 0) {
      return;
    }
    const frames = pending;
    pending = [];
    apply(frames);
  };

  return {
    push: (frame) => {
      pending.push(frame);
      timer ??= setTimeout(flush, delayMs);
    },
    flush,
    cancel: () => {
      if (timer) {
        clearTimeout(timer);
        timer = null;
      }
      pending = [];
    },
  };
}

/** Joins consecutive frames of the same type whose text simply concatenates, keeping everything else in order. */
export function mergeTextFrames<T extends { type?: string; text?: string }>(frames: T[], mergeable: ReadonlySet<string>): T[] {
  const merged: T[] = [];
  for (const frame of frames) {
    const last = merged[merged.length - 1];
    if (last && frame.type && last.type === frame.type && mergeable.has(frame.type)) {
      merged[merged.length - 1] = { ...last, text: (last.text ?? "") + (frame.text ?? "") };
    } else {
      merged.push(frame);
    }
  }
  return merged;
}
