export type Inline =
  | { type: "text"; value: string }
  | { type: "code"; value: string }
  | { type: "emphasis"; children: Inline[] }
  | { type: "strong"; children: Inline[] }
  | { type: "link"; href: string; title?: string; children: Inline[] }
  | { type: "image"; src: string; alt: string; title?: string }
  | { type: "break" }
  | { type: "softbreak" };

export type Block =
  | { type: "heading"; level: 1 | 2 | 3 | 4 | 5 | 6; content: string }
  | { type: "paragraph"; content: string }
  | { type: "code"; lang?: string; value: string }
  | { type: "blockquote"; children: Block[] }
  | { type: "list"; ordered: boolean; start: number; tight: boolean; items: Block[][] }
  | { type: "thematicBreak" };

export interface LinkDefinition {
  href: string;
  title?: string;
}

/** Link reference definitions (`[label]: url "title"`), keyed by normalised label. */
export type LinkDefinitions = Map<string, LinkDefinition>;

export function normalizeLabel(label: string): string {
  return label.trim().replace(/\s+/g, " ").toLowerCase();
}
