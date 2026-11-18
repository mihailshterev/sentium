import type { VaultLink } from "@/features/vault/types/vault";

/** Href scheme the note renderer turns into in-page navigation; never leaves the portal. */
export const WIKILINK_SCHEME = "vault-note:";

const WIKILINK = /(!?)\[\[([^[\]\n]+?)\]\]/g;
const FENCE = /^(```|~~~)/;

/**
 * Rewrites `[[target#heading|alias]]` (and `![[embeds]]`) into markdown links the renderer can follow, keyed by the
 * target as written. Fenced code blocks are left alone, like Obsidian does.
 */
export function wikilinksToMarkdown(content: string): string {
  let inFence = false;

  return content
    .split("\n")
    .map((line) => {
      if (FENCE.test(line.trimStart())) {
        inFence = !inFence;
        return line;
      }

      return inFence ? line : line.replace(WIKILINK, (_match, _embed: string, inner: string) => toLink(inner));
    })
    .join("\n");
}

/** The note a rewritten link points to, using the server's resolution of that target. */
export function resolveWikilink(href: string, links: VaultLink[]): VaultLink | undefined {
  const target = decodeURIComponent(href.slice(WIKILINK_SCHEME.length)).toLowerCase();
  return links.find((link) => link.text.toLowerCase() === target);
}

function toLink(inner: string): string {
  const pipe = inner.indexOf("|");
  const alias = pipe >= 0 ? inner.slice(pipe + 1).trim() : "";
  const reference = (pipe >= 0 ? inner.slice(0, pipe) : inner).replace(/\\$/, "");
  const hash = reference.indexOf("#");
  const target = (hash >= 0 ? reference.slice(0, hash) : reference).trim();
  const heading = hash >= 0 ? reference.slice(hash + 1).trim() : "";

  const label = alias || [target.split("/").pop() ?? target, heading].filter(Boolean).join(" › ");
  if (!target) {
    return escapeLabel(label);
  }

  return `[${escapeLabel(label)}](${WIKILINK_SCHEME}${encodeURIComponent(target)})`;
}

function escapeLabel(label: string): string {
  return label.replace(/[[\]]/g, "\\$&");
}
