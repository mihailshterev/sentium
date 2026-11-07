import { RefreshCw } from "lucide-react";
import styles from "../inventory.module.css";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import type { KnowledgeStatus } from "../types/inventory";

const STATUS: Record<KnowledgeStatus, { tone: BadgeTone; label: string; title: string }> = {
  NotShared: { tone: "neutral", label: "Private", title: "Assistants cannot see this item." },
  Pending: { tone: "info", label: "Syncing", title: "Updating what assistants know about this item…" },
  Synced: { tone: "success", label: "Shared", title: "Assistants can find this item." },
  Failed: { tone: "danger", label: "Sync failed", title: "The knowledge base could not be updated." },
};

interface KnowledgeStatusBadgeProps {
  status: KnowledgeStatus;
  error?: string | null;
  onRetry?: () => void;
  isRetrying?: boolean;
}

export function KnowledgeStatusBadge({ status, error, onRetry, isRetrying }: KnowledgeStatusBadgeProps) {
  const meta = STATUS[status];

  return (
    <span className={styles.statusBadge}>
      <Badge tone={meta.tone} dot title={error ?? meta.title} data-testid="knowledge-status">
        {meta.label}
      </Badge>
      {status === "Failed" && onRetry && (
        <Button variant="ghost" size="sm" icon={<RefreshCw size={13} />} loading={isRetrying} onClick={onRetry}>
          Retry
        </Button>
      )}
    </span>
  );
}
