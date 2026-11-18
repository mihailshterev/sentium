export interface VaultSummary {
  id: string;
  name: string;
  /** The paired Device that syncs the vault; null once that Device is removed. */
  deviceName: string | null;
  noteCount: number;
  linkCount: number;
  unresolvedLinkCount: number;
  orphanCount: number;
  pendingIndexCount: number;
  failedIndexCount: number;
  lastSyncAt: string | null;
}

export interface VaultNoteSummary {
  id: string;
  path: string;
  title: string;
  tags: string[];
  wordCount: number;
  updatedAt: string;
  /** Whether the knowledge base holds the current version of the note. */
  indexed: boolean;
}

export interface VaultNoteRef {
  id: string;
  path: string;
  title: string;
  obsidianUri: string;
  tags: string[];
}

export interface VaultLink {
  text: string;
  heading: string | null;
  isEmbed: boolean;
  /** null while no synced note matches the link. */
  target: VaultNoteRef | null;
}

export interface VaultNote {
  id: string;
  path: string;
  title: string;
  obsidianUri: string;
  content: string;
  tags: string[];
  aliases: string[];
  frontmatter: Record<string, unknown> | null;
  wordCount: number;
  updatedAt: string;
  indexedAt: string | null;
  links: VaultLink[];
  backlinks: VaultNoteRef[];
}

export interface VaultTagCount {
  tag: string;
  count: number;
}

export interface VaultGraphNode {
  id: string;
  title: string;
  path: string;
  folder: string;
  tag: string | null;
  degree: number;
}

export interface VaultGraphEdge {
  source: string;
  target: string;
}

export interface VaultGraph {
  nodes: VaultGraphNode[];
  edges: VaultGraphEdge[];
  /** More notes matched than the node limit; the best-linked ones are shown. */
  truncated: boolean;
}

export interface VaultNotesFilter {
  search?: string;
  tag?: string;
  folder?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
