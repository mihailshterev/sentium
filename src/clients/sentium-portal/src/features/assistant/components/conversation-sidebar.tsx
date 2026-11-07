import { useState } from "react";
import { ChevronDown, ChevronRight, FileText, FolderOpen, MessageSquare, Pencil, Plus, Trash2 } from "lucide-react";
import styles from "../assistant.module.css";
import { Button } from "@/shared/ui/button";
import { InfiniteScrollSentinel } from "@/shared/ui/infinite-scroll-sentinel";
import { SearchInput } from "@/shared/ui/form-controls";
import { InlineTitleEdit } from "@/shared/ui/inline-title-edit";
import { formatConversationTime } from "@/features/assistant/utils/format-conversation-time";
import type { ConversationSummary } from "@/features/assistant/types/assistant";
import type { Workspace, WorkspaceFile } from "@/features/workspaces/types/workspace";

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
  wsContextOpen: boolean;
  workspaces: Workspace[];
  expandedWorkspace: string | null;
  expandedWorkspaceFiles: WorkspaceFile[];
  onNewConversation: () => void;
  onLoadConversation: (conv: ConversationSummary) => void;
  onDeleteConversation: (id: string, e: React.MouseEvent) => void;
  onRenameConversation: (id: string, title: string) => void;
  onToggleWsContext: () => void;
  onToggleExpandWorkspace: (wsId: string) => void;
  onInjectWorkspaceContext: (ws: Workspace) => void;
  onInjectFileContext: (fileName: string, fileId: string) => void;
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
  wsContextOpen,
  workspaces,
  expandedWorkspace,
  expandedWorkspaceFiles,
  onNewConversation,
  onLoadConversation,
  onDeleteConversation,
  onRenameConversation,
  onToggleWsContext,
  onToggleExpandWorkspace,
  onInjectWorkspaceContext,
  onInjectFileContext,
}: ConversationSidebarProps) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const searching = search.trim().length > 0;

  return (
    <aside className={styles.sidebar} data-open={isOpen || undefined} aria-label="Conversations">
      <div className={styles.sidebarHeader}>
        <div className={styles.sidebarHeading}>
          <span className={styles.sidebarTitle}>Conversations</span>
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
      </div>

      <div className={styles.convList}>
        {conversations.length === 0 && (
          <div className={styles.convEmpty}>
            <MessageSquare size={20} aria-hidden="true" />
            <span>{searching ? `No conversations match “${search.trim()}”` : "No conversations yet"}</span>
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
                const time = formatConversationTime(conv.createdAt);

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
                            <time className={styles.convTime} dateTime={conv.createdAt}>
                              {time}
                            </time>
                          )}
                        </button>
                        <div className={styles.convActions}>
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

      <div className={styles.wsSection}>
        <button type="button" className={styles.wsToggle} onClick={onToggleWsContext} aria-expanded={wsContextOpen}>
          <FolderOpen size={14} aria-hidden="true" />
          <span>Workspace Context</span>
          <ChevronDown size={14} className={wsContextOpen ? styles.chevronOpen : styles.chevron} />
        </button>
        {wsContextOpen && (
          <div className={styles.wsList}>
            {workspaces.length === 0 && <p className={styles.wsEmpty}>No workspaces found.</p>}
            {workspaces.map((ws) => (
              <div key={ws.id} className={styles.wsItem}>
                <div className={styles.wsRow}>
                  <button
                    type="button"
                    className={styles.wsName}
                    onClick={() => onToggleExpandWorkspace(ws.id)}
                    title="Expand workspace files"
                    aria-expanded={expandedWorkspace === ws.id}
                  >
                    <ChevronRight
                      size={13}
                      className={expandedWorkspace === ws.id ? styles.chevronRight90 : styles.chevronRight}
                    />
                    <span>{ws.name}</span>
                  </button>
                  <button
                    type="button"
                    className={styles.wsInject}
                    onClick={() => onInjectWorkspaceContext(ws)}
                    title="Insert workspace reference into message"
                  >
                    <Plus size={14} />
                  </button>
                </div>
                {expandedWorkspace === ws.id && (
                  <div className={styles.wsFiles}>
                    {expandedWorkspaceFiles.length === 0 && <p className={styles.wsEmpty}>No files.</p>}
                    {expandedWorkspaceFiles.map((f) => (
                      <button
                        key={f.id}
                        type="button"
                        className={styles.wsFile}
                        onClick={() => onInjectFileContext(f.fileName, f.id)}
                        title={`Insert file reference: ${f.fileName}`}
                        disabled={f.processingStatus !== "Completed"}
                      >
                        <FileText size={13} aria-hidden="true" />
                        <span>{f.fileName}</span>
                        {f.processingStatus !== "Completed" && (
                          <span className={styles.wsFileStatus}>{f.processingStatus}</span>
                        )}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </aside>
  );
}
