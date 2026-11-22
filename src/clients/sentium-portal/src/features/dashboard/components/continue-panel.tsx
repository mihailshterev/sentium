import { Link } from "react-router";
import { MessageSquare } from "lucide-react";
import styles from "../dashboard.module.css";
import type { ConversationSummary } from "@/features/assistant/types/assistant";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Skeleton } from "@/shared/ui/skeleton";
import { formatRelativeTime } from "@/shared/utils/formatters";

const VISIBLE_CONVERSATIONS = 5;

interface ContinuePanelProps {
  conversations: ConversationSummary[];
  loading: boolean;
}

export function ContinuePanel({ conversations, loading }: ContinuePanelProps) {
  return (
    <Card padding="none" as="section" aria-label="Continue where you left off">
      <CardHeader
        title="Continue where you left off"
        actions={
          <Link to="/assistant" className={styles.headerLink}>
            All conversations
          </Link>
        }
      />

      {loading ? (
        <div className={styles.listSkeleton} aria-busy="true">
          <Skeleton height={32} />
          <Skeleton height={32} />
          <Skeleton height={32} />
        </div>
      ) : conversations.length === 0 ? (
        <EmptyState compact icon={<MessageSquare />} title="No conversations yet" hint="Start one with the assistant." />
      ) : (
        <ul className={styles.rowList}>
          {conversations.slice(0, VISIBLE_CONVERSATIONS).map((conversation) => (
            <li key={conversation.id}>
              <Link
                to={`/assistant/${conversation.id}`}
                className={styles.continueRow}
                aria-label={`${conversation.title || "Untitled conversation"}, ${formatRelativeTime(conversation.createdAt)}`}
              >
                <MessageSquare size={15} className={styles.rowIcon} aria-hidden="true" />
                <span className={styles.rowBody}>
                  <span className={styles.rowTitle}>{conversation.title || "Untitled conversation"}</span>
                  {(conversation.agentName || conversation.workspaceName) && (
                    <span className={styles.rowDetail}>
                      {[conversation.agentName, conversation.workspaceName].filter(Boolean).join(" · ")}
                    </span>
                  )}
                </span>
                <span className={styles.rowMeta}>{formatRelativeTime(conversation.createdAt)}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
