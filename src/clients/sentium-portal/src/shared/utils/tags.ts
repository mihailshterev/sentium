/** Canonical tag form, matching the server's TagNormalizer: trimmed, single-spaced, lower-case. */
export const normalizeTag = (tag: string) => tag.trim().replace(/\s+/g, " ").toLowerCase();
