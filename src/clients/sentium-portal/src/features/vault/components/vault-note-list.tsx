import { useDeferredValue, useState } from "react";
import { ChevronLeft, ChevronRight, FileSearch } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { SearchInput, Select } from "@/shared/ui/form-controls";
import { SkeletonText } from "@/shared/ui/skeleton";
import { useVaultNotes, useVaultTags } from "@/features/vault/hooks/useVault";
import styles from "../vault.module.css";

const PAGE_SIZE = 50;

interface VaultNoteListProps {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

export function VaultNoteList({ selectedId, onSelect }: VaultNoteListProps) {
  const [search, setSearch] = useState("");
  const [tag, setTag] = useState("");
  const [page, setPage] = useState(1);
  const deferredSearch = useDeferredValue(search.trim());

  const { data: tags = [] } = useVaultTags(true);
  const { data, isLoading } = useVaultNotes({ search: deferredSearch, tag, page, pageSize: PAGE_SIZE }, true);

  const notes = data?.items ?? [];
  const totalPages = data?.totalPages ?? 0;

  return (
    <Card className={styles.listPanel}>
      <div className={styles.listFilters}>
        <SearchInput
          controlSize="sm"
          placeholder="Filter by title or path"
          aria-label="Filter notes"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <Select
          controlSize="sm"
          aria-label="Tag"
          value={tag}
          onChange={(e) => {
            setTag(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All tags</option>
          {tags.map((t) => (
            <option key={t.tag} value={t.tag}>
              #{t.tag} ({t.count})
            </option>
          ))}
        </Select>
      </div>

      {isLoading && <SkeletonText lines={10} className={styles.listSkeleton} />}

      {!isLoading && notes.length === 0 && (
        <EmptyState compact icon={<FileSearch />} title="No matching notes" hint="Try another filter or tag." />
      )}

      <ul className={styles.noteList} aria-label="Notes">
        {notes.map((note) => (
          <li key={note.id}>
            <button
              type="button"
              className={styles.noteRow}
              aria-current={note.id === selectedId ? "true" : undefined}
              onClick={() => onSelect(note.id)}
            >
              <span className={styles.noteRowTitle}>{note.title}</span>
              <span className={styles.notePath}>{note.path}</span>
              <span className={styles.noteRowMeta}>
                {note.tags.slice(0, 3).map((t) => (
                  <Badge key={t} tone="cyan">
                    #{t}
                  </Badge>
                ))}
                {!note.indexed && <Badge tone="amber">indexing</Badge>}
              </span>
            </button>
          </li>
        ))}
      </ul>

      {totalPages > 1 && (
        <div className={styles.pager}>
          <Button size="sm" iconOnly aria-label="Previous page" icon={<ChevronLeft />} disabled={page <= 1} onClick={() => setPage(page - 1)} />
          <span className={styles.muted}>
            {page} / {totalPages}
          </span>
          <Button size="sm" iconOnly aria-label="Next page" icon={<ChevronRight />} disabled={page >= totalPages} onClick={() => setPage(page + 1)} />
        </div>
      )}
    </Card>
  );
}
