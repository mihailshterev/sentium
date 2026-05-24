import {
  Plus,
  MessageSquare,
  Trash2,
  FolderOpen,
  FileText,
  ChevronDown,
  ChevronRight,
  Cpu,
  Loader2,
} from "lucide-react";
import styles from "../assistant.module.scss";
import type { ConversationSummary } from "../../../types/assistant";
import type { Workspace, WorkspaceFile } from "../../../types/workspace";
import ModelSelector from "../../../components/ui/model-selector";

interface ConversationGroup {
  label: string;
  items: ConversationSummary[];
}

interface ConversationSidebarProps {
  isOpen: boolean;
  conversations: ConversationSummary[];
  conversationGroups: ConversationGroup[];
  activeConversationId: string | null;
  model: string;
  models: string[];
  isCreating: boolean;
  wsContextOpen: boolean;
  workspaces: Workspace[];
  expandedWorkspace: string | null;
  expandedWorkspaceFiles: WorkspaceFile[];
  onNewConversation: () => void;
  onLoadConversation: (conv: ConversationSummary) => void;
  onDeleteConversation: (id: string, e: React.MouseEvent) => void;
  onSetModel: (model: string) => void;
  onToggleWsContext: () => void;
  onToggleExpandWorkspace: (wsId: string) => void;
  onInjectWorkspaceContext: (ws: Workspace) => void;
  onInjectFileContext: (fileName: string, fileId: string) => void;
}

const ConversationSidebar = ({
  isOpen,
  conversations,
  conversationGroups,
  activeConversationId,
  model,
  models,
  isCreating,
  wsContextOpen,
  workspaces,
  expandedWorkspace,
  expandedWorkspaceFiles,
  onNewConversation,
  onLoadConversation,
  onDeleteConversation,
  onSetModel,
  onToggleWsContext,
  onToggleExpandWorkspace,
  onInjectWorkspaceContext,
  onInjectFileContext,
}: ConversationSidebarProps) => {
  return (
    <aside className={`${styles.sidebar} ${isOpen ? styles.sidebarOpen : ""}`}>
      <div className={styles.sidebarHeader}>
        <span className={styles.sidebarTitle}>Conversations</span>
        <button
          className={styles.newChatBtn}
          onClick={onNewConversation}
          title="New conversation"
          disabled={isCreating}
        >
          {isCreating ? <Loader2 size={13} /> : <Plus size={13} />}
        </button>
      </div>

      <div className={styles.modelSelector}>
        <Cpu size={12} />
        <ModelSelector
          className={models.length > 0 ? styles.modelSelect : styles.modelInput}
          models={models}
          value={model}
          onChange={onSetModel}
          placeholder="model name..."
        />
      </div>

      <div className={styles.convList}>
        {conversations.length === 0 && (
          <div className={styles.convEmpty}>
            <MessageSquare size={22} className={styles.convEmptyIcon} />
            <span>No conversations yet</span>
          </div>
        )}
        {conversationGroups.map((group) => (
          <div key={group.label}>
            <div className={styles.convGroupLabel}>{group.label}</div>
            {group.items.map((conv) => (
              <div
                key={conv.id}
                className={`${styles.convItem} ${activeConversationId === conv.id ? styles.convItemActive : ""}`}
                onClick={() => onLoadConversation(conv)}
              >
                <MessageSquare size={12} className={styles.convIcon} />
                <div className={styles.convInfo}>
                  <span className={styles.convTitle}>{conv.title}</span>
                </div>
                <button
                  className={styles.convDelete}
                  onClick={(e) => onDeleteConversation(conv.id, e)}
                  title="Delete conversation"
                >
                  <Trash2 size={11} />
                </button>
              </div>
            ))}
          </div>
        ))}
      </div>

      <div className={styles.wsContextSection}>
        <button className={styles.wsContextToggle} onClick={onToggleWsContext}>
          <FolderOpen size={11} />
          <span>Workspace Context</span>
          <ChevronDown size={11} className={`${styles.wsChevron} ${wsContextOpen ? styles.wsChevronOpen : ""}`} />
        </button>
        {wsContextOpen && (
          <div className={styles.wsContextList}>
            {workspaces.length === 0 && <p className={styles.wsContextEmpty}>No workspaces found.</p>}
            {workspaces.map((ws) => (
              <div key={ws.id} className={styles.wsContextItem}>
                <button
                  className={styles.wsContextName}
                  onClick={() => onToggleExpandWorkspace(ws.id)}
                  title="Expand workspace files"
                >
                  <FolderOpen size={11} className={styles.wsContextIcon} />
                  <span>{ws.name}</span>
                  <ChevronRight
                    size={10}
                    className={`${styles.wsExpandChevron} ${expandedWorkspace === ws.id ? styles.wsExpandChevronOpen : ""}`}
                  />
                </button>
                <button
                  className={styles.wsInjectBtn}
                  onClick={() => onInjectWorkspaceContext(ws)}
                  title="Insert workspace reference into message"
                >
                  +
                </button>
                {expandedWorkspace === ws.id && (
                  <div className={styles.wsFileList}>
                    {expandedWorkspaceFiles.length === 0 && <p className={styles.wsContextEmpty}>No files.</p>}
                    {expandedWorkspaceFiles.map((f) => (
                      <button
                        key={f.id}
                        className={styles.wsFileItem}
                        onClick={() => onInjectFileContext(f.fileName, f.id)}
                        title={`Insert file reference: ${f.fileName}`}
                        disabled={f.processingStatus !== "Completed"}
                      >
                        <FileText size={10} />
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
};

export default ConversationSidebar;
