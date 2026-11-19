import { useMemo } from "react";
import { Markdown } from "@/shared/markdown";
import { ExternalLink, FileText, Link2, Unlink } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { SkeletonText } from "@/shared/ui/skeleton";
import { markdownUrlTransform } from "@/shared/utils/markdown-url";
import { useVaultNote } from "@/features/vault/hooks/useVault";
import type { VaultLink } from "@/features/vault/types/vault";
import { WIKILINK_SCHEME, resolveWikilink, wikilinksToMarkdown } from "@/features/vault/utils/wikilinks";
import styles from "../vault.module.css";

interface VaultNotePanelProps {
  noteId: string | null;
  onOpenNote: (id: string) => void;
}

/** One note, rendered: its wikilinks navigate within the page, and "open in Obsidian" hands off to the app. */
export function VaultNotePanel({ noteId, onOpenNote }: VaultNotePanelProps) {
  const { data: note, isLoading, error } = useVaultNote(noteId);
  const markdown = useMemo(() => (note ? wikilinksToMarkdown(stripFrontmatter(note.content)) : ""), [note]);

  if (noteId === null) {
    return (
      <Card className={styles.notePanel}>
        <EmptyState icon={<FileText />} title="Pick a note" hint="Choose a note from the list or the graph to read it here." />
      </Card>
    );
  }

  if (isLoading || !note) {
    return (
      <Card className={styles.notePanel} padded aria-busy="true">
        {error ? <p className={styles.muted}>The note could not be loaded.</p> : <SkeletonText lines={8} />}
      </Card>
    );
  }

  const unresolved = note.links.filter((link) => link.target === null);

  return (
    <Card as="article" className={styles.notePanel} padded data-testid="vault-note-panel">
      <header className={styles.noteHeader}>
        <div className={styles.noteHeading}>
          <h2 className={styles.noteTitle}>{note.title}</h2>
          <span className={styles.notePath}>{note.path}</span>
        </div>
        <a className={styles.openLink} href={note.obsidianUri}>
          <ExternalLink size={14} aria-hidden="true" />
          Open in Obsidian
        </a>
      </header>

      {(note.tags.length > 0 || note.aliases.length > 0) && (
        <div className={styles.chips}>
          {note.tags.map((tag) => (
            <Badge key={tag} tone="cyan">
              #{tag}
            </Badge>
          ))}
          {note.aliases.map((alias) => (
            <Badge key={alias}>aka {alias}</Badge>
          ))}
        </div>
      )}

      <div className={styles.markdown} data-testid="vault-note-content">
        <Markdown
          urlTransform={noteUrlTransform}
          components={{ a: ({ href, children }) => renderLink(href, children, note.links, onOpenNote) }}
        >
          {markdown}
        </Markdown>
      </div>

      <section className={styles.linkSection} aria-label="Backlinks">
        <h3 className={styles.sectionLabel}>
          <Link2 size={13} aria-hidden="true" /> backlinks · {note.backlinks.length}
        </h3>
        {note.backlinks.length === 0 ? (
          <p className={styles.muted}>No other note links here.</p>
        ) : (
          <ul className={styles.linkList}>
            {note.backlinks.map((backlink) => (
              <li key={backlink.id}>
                <button type="button" className={styles.linkButton} onClick={() => onOpenNote(backlink.id)}>
                  {backlink.title}
                </button>
                <span className={styles.notePath}>{backlink.path}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {unresolved.length > 0 && (
        <section className={styles.linkSection} aria-label="Unresolved links">
          <h3 className={styles.sectionLabel}>
            <Unlink size={13} aria-hidden="true" /> not created yet · {unresolved.length}
          </h3>
          <div className={styles.chips}>
            {unresolved.map((link) => (
              <Badge key={`${link.text}#${link.heading ?? ""}`} tone="amber">
                {link.text}
              </Badge>
            ))}
          </div>
        </section>
      )}
    </Card>
  );
}

function renderLink(href: string | undefined, children: React.ReactNode, links: VaultLink[], onOpenNote: (id: string) => void) {
  if (href?.startsWith(WIKILINK_SCHEME)) {
    const target = resolveWikilink(href, links)?.target;
    return target ? (
      <button type="button" className={styles.wikilink} onClick={() => onOpenNote(target.id)}>
        {children}
      </button>
    ) : (
      <span className={styles.wikilinkMissing} title="This note does not exist yet">
        {children}
      </span>
    );
  }

  return (
    <a href={href} target="_blank" rel="noreferrer noopener">
      {children}
    </a>
  );
}

function noteUrlTransform(url: string): string {
  return url.startsWith(WIKILINK_SCHEME) ? url : markdownUrlTransform(url);
}

/** Frontmatter is shown as tags and aliases above the note, not as a stray table of YAML. */
function stripFrontmatter(content: string): string {
  if (!content.startsWith("---")) {
    return content;
  }

  const end = content.indexOf("\n---", 3);
  return end < 0 ? content : content.slice(content.indexOf("\n", end + 4) + 1 || content.length);
}
