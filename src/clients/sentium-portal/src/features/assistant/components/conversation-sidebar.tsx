import { ChevronDown, ChevronRight, FileText, FolderOpen, MessageSquare, Plus, Trash2 } from "lucide-react";
import styles from "../assistant.module.css";
import { Button } from "@/shared/ui/button";
import { InfiniteScrollSentinel } from "@/shared/ui/infinite-scroll-sentinel";
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
  onToggleWsContext: () => void;
  onToggleExpandWorkspace: (wsId: string) => void;
  onInjectWorkspaceContext: (ws: Workspace) => void;
  onInjectFileContext: (fileName: string, fileId: string) => void;
}

export function ConversationSidebar({
  isOpen,
  conversations,
  conversationGroups,
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
  onToggleWsContext,
  onToggleExpandWorkspace,
  onInjectWorkspaceContext,
  onInjectFileContext,
}: ConversationSidebarProps) {
  return (
    <aside className={styles.sidebar} data-open={isOpen || undefined} aria-label="Conversations">
      <div className={styles.sidebarHeader}>
        <span className={styles.sidebarTitle}>Conversations</span>
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

      <div className={styles.convList}>
        {conversations.length === 0 && (
          <div className={styles.convEmpty}>
            <MessageSquare size={20} aria-hidden="true" />
            <span>No conversations yet</span>
          </div>
        )}
        {conversationGroups.map((group) => (
          <div key={group.label} className={styles.convGroup}>
            <div className={styles.convGroupLabel}>{group.label}</div>
            <ul className={styles.convItems}>
              {group.items.map((conv) => (
                <li
                  key={conv.id}
                  className={`${styles.convItem} ${activeConversationId === conv.id ? styles.convItemActive : ""}`}
                >
                  <button
                    type="button"
                    className={styles.convSelect}
                    onClick={() => onLoadConversation(conv)}
                    aria-current={activeConversationId === conv.id || undefined}
                  >
                    <span className={styles.convTitle}>{conv.title}</span>
                  </button>
                  <button
                    type="button"
                    className={styles.convDelete}
                    onClick={(e) => onDeleteConversation(conv.id, e)}
                    title="Delete conversation"
                  >
                    <Trash2 size={14} />
                  </button>
                </li>
              ))}
            </ul>
          </div>
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
