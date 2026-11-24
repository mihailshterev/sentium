import { useState } from "react";
import { Plus, UserRoundSearch } from "lucide-react";
import styles from "../knowledge-base.module.css";
import { useUserMemories } from "../hooks/useUserMemories";
import { MAX_MEMORY_LENGTH } from "../types/user-memory";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Textarea } from "@/shared/ui/form-controls";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { MemoryCard } from "./memory-card";

const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });

/** What agents remember about the signed-in user: add, correct and forget facts one by one. */
export function MemoriesTab() {
  const {
    memories,
    isLoading,
    error,
    addMemory,
    isAdding,
    addError,
    updateMemory,
    updatingId,
    deleteMemory,
    deletingId,
    actionError,
  } = useUserMemories();
  const [draft, setDraft] = useState("");

  const add = async () => {
    if (!draft.trim()) {
      return;
    }
    try {
      await addMemory(draft.trim());
      setDraft("");
    } catch {
      // shown through addError
    }
  };

  return (
    <Card padding="none" as="section" data-testid="memories-tab">
      <CardHeader
        icon={<UserRoundSearch />}
        title="My memories"
        description="Facts and preferences agents saved about you. Only you can see them; correct or forget any of them here."
        actions={!isLoading && <Badge tone="neutral">{memories.length} saved</Badge>}
      />

      <div className={styles.cardBody}>
        <form
          className={styles.memoryForm}
          onSubmit={(e) => {
            e.preventDefault();
            void add();
          }}
        >
          <Textarea
            aria-label="New memory"
            rows={2}
            maxLength={MAX_MEMORY_LENGTH}
            placeholder="Something agents should always know, e.g. “I am vegetarian.”"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
          <Button type="submit" variant="primary" icon={<Plus size={15} />} loading={isAdding} disabled={!draft.trim()}>
            Remember
          </Button>
        </form>
        {addError && <StatusMessage variant="error" message={addError.message} />}
        {actionError && <StatusMessage variant="error" message={actionError.message} />}
      </div>

      {isLoading && (
        <div className={styles.cardBody} aria-busy="true">
          <SkeletonText lines={3} />
        </div>
      )}

      {error && <StatusMessage variant="error" message={error.message} className={styles.cardBody} />}

      {!isLoading && !error && memories.length === 0 && (
        <EmptyState
          icon={<UserRoundSearch />}
          title="Nothing remembered yet"
          hint="Ask an agent to remember something about you, or add it above."
        />
      )}

      {memories.length > 0 && (
        <ul className={styles.learningList}>
          {memories.map((m) => (
            <MemoryCard
              key={m.id}
              memory={m}
              isSaving={updatingId === m.id}
              isDeleting={deletingId === m.id}
              onSave={(content) => updateMemory({ id: m.id, content })}
              onDelete={() => deleteMemory(m.id)}
              formatDate={formatDate}
            />
          ))}
        </ul>
      )}
    </Card>
  );
}
