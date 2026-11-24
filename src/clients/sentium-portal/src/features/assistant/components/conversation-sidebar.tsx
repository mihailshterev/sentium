import { useState } from "react";
import { Archive, ArchiveRestore, MessageSquare, Pencil, Pin, PinOff, Plus, Trash2 } from "lucide-react";
import styles from "../assistant.module.css";
import { Button } from "@/shared/ui/button";
import { InfiniteScrollSentinel } from "@/shared/ui/infinite-scroll-sentinel";
import { SearchInput } from "@/shared/ui/form-controls";
import { InlineTitleEdit } from "@/shared/ui/inline-title-edit";
import { formatConversationTime } from "@/features/assistant/utils/format-conversation-time";
import type { ConversationSummary } from "@/features/assistant/types/assistant";

interface ConversationGroup {
  label: string;
  items: ConversationSummary[];
}

interface ConversationSidebarProps {
  isOpen: boolean;
  conversations: ConversationSummary[];
  conversationGroups: ConversationGroup[];
  totalCount: number;
  search: string;
  onSearchChange: (value: string) => void;
  activeConversationId: string | null;
  isCreating: boolean;
  hasMoreConversations?: boolean;
  isLoadingMoreConversations: boolean;
  onLoadMoreConversations: () => void;
  onNewConversation: () => void;
  onLoadConversation: (conv: ConversationSummary) => void;
  onDeleteConversation: (id: string, e: React.MouseEvent) => void;
  onRenameConversation: (id: string, title: string) => void;
  /** Lists the archived conversations instead of the active ones. */
  showArchived: boolean;
  onShowArchivedChange: (show: boolean) => void;
  onSetPinned: (id: string, pinned: boolean) => void;
  onSetArchived: (id: string, archived: boolean) => void;
}

export function ConversationSidebar({
  isOpen,
  conversations,
  conversationGroups,
  totalCount,
  search,
  onSearchChange,
  activeConversationId,
  isCreating,
  hasMoreConversations,
  isLoadingMoreConversations,
  onLoadMoreConversations,
  onNewConversation,
  onLoadConversation,
  onDeleteConversation,
  onRenameConversation,
  showArchived,
  onShowArchivedChange,
  onSetPinned,
  onSetArchived,
}: ConversationSidebarProps) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const searching = search.trim().length > 0;

  return (
    <aside className={styles.sidebar} data-open={isOpen || undefined} aria-label="Conversations">
      <div className={styles.sidebarHeader}>
        <div className={styles.sidebarHeading}>
          <span className={styles.sidebarTitle}>{showArchived ? "Archived" : "Conversations"}</span>
          {totalCount > 0 && <span className={styles.sidebarCount}>{totalCount}</span>}
        </div>
        <Button
          variant="secondary"
          size="sm"
          icon={<Plus size={15} />}
          onClick={onNewConversation}
          loading={isCreating}
          title="New conversation"
        >
          New
        </Button>
      </div>

      <div className={styles.sidebarSearch}>
        <SearchInput
          controlSize="sm"
          placeholder="Search conversations"
          aria-label="Search conversations"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Escape" && search) {
              e.preventDefault();
              onSearchChange("");
            }
          }}
        />
        <Button
          variant="ghost"
          size="sm"
          iconOnly
          icon={showArchived ? <MessageSquare size={15} /> : <Archive size={15} />}
          aria-pressed={showArchived}
          title={showArchived ? "Back to conversations" : "Show archived conversations"}
          onClick={() => onShowArchivedChange(!showArchived)}
        />
      </div>

      <div className={styles.convList}>
        {conversations.length === 0 && (
          <div className={styles.convEmpty}>
            <MessageSquare size={20} aria-hidden="true" />
            <span>
              {searching
                ? `No conversations match “${search.trim()}”`
                : showArchived
                  ? "No archived conversations"
                  : "No conversations yet"}
            </span>
          </div>
        )}
        {conversationGroups.map((group) => (
          <section key={group.label} className={styles.convGroup} aria-label={group.label}>
            <div className={styles.convGroupLabel}>
              <span>{group.label}</span>
              <span className={styles.convGroupRule} aria-hidden="true" />
              <span className={styles.convGroupCount}>{group.items.length}</span>
            </div>
            <ul className={styles.convItems}>
              {group.items.map((conv) => {
                const active = activeConversationId === conv.id;
                const lastActive = conv.lastActivityAt ?? conv.createdAt;
                const time = formatConversationTime(lastActive);

                return (
                  <li key={conv.id} className={styles.convItem} data-active={active || undefined}>
                    {editingId === conv.id ? (
                      <div className={styles.convEditing}>
                        <InlineTitleEdit
                          value={conv.title}
                          ariaLabel="Conversation title"
                          onSave={(title) => {
                            setEditingId(null);
                            onRenameConversation(conv.id, title);
                          }}
                          onCancel={() => setEditingId(null)}
                        />
                      </div>
                    ) : (
                      <>
                        <button
                          type="button"
                          className={styles.convSelect}
                          onClick={() => onLoadConversation(conv)}
                          aria-current={active || undefined}
                          title={conv.title}
                        >
                          <span className={styles.convTitle}>{conv.title}</span>
                          {time && (
                            <time className={styles.convTime} dateTime={lastActive}>
                              {time}
                            </time>
                          )}
                        </button>
                        <div className={styles.convActions}>
                          {!showArchived && (
                            <button
                              type="button"
                              className={styles.convAction}
                              onClick={(e) => {
                                e.stopPropagation();
                                onSetPinned(conv.id, !conv.isPinned);
                              }}
                              title={conv.isPinned ? "Unpin conversation" : "Pin conversation"}
                              aria-label={`${conv.isPinned ? "Unpin" : "Pin"} ${conv.title}`}
                            >
                              {conv.isPinned ? <PinOff size={13} /> : <Pin size={13} />}
                            </button>
                          )}
                          <button
                            type="button"
                            className={styles.convAction}
                            onClick={(e) => {
                              e.stopPropagation();
                              onSetArchived(conv.id, !showArchived);
                            }}
                            title={showArchived ? "Unarchive conversation" : "Archive conversation"}
                            aria-label={`${showArchived ? "Unarchive" : "Archive"} ${conv.title}`}
                          >
                            {showArchived ? <ArchiveRestore size={13} /> : <Archive size={13} />}
                          </button>
                          <button
                            type="button"
                            className={styles.convAction}
                            onClick={(e) => {
                              e.stopPropagation();
                              setEditingId(conv.id);
                            }}
                            title="Rename conversation"
                            aria-label={`Rename ${conv.title}`}
                          >
                            <Pencil size={13} />
                          </button>
                          <button
                            type="button"
                            className={`${styles.convAction} ${styles.convActionDanger}`}
                            onClick={(e) => onDeleteConversation(conv.id, e)}
                            title="Delete conversation"
                          >
                            <Trash2 size={13} />
                          </button>
                        </div>
                      </>
                    )}
                  </li>
                );
              })}
            </ul>
          </section>
        ))}

        <InfiniteScrollSentinel
          hasMore={!!hasMoreConversations}
          isLoading={isLoadingMoreConversations}
          onLoadMore={onLoadMoreConversations}
        />
      </div>

    </aside>
  );
}
