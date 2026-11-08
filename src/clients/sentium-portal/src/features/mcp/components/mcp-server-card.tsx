import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity, ChevronDown, Container, Globe, Pencil, RefreshCw, Trash2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { StatusMessage } from "@/shared/ui/status-message";
import { fetchMcpServerStatus } from "../api/mcp.service";
import type { McpServer, McpToolUpdate } from "../types/mcp";
import { McpToolsTable } from "./mcp-tools-table";
import styles from "../mcp.module.css";

interface McpServerCardProps {
  server: McpServer;
  syncing: boolean;
  updatingTools: boolean;
  onSync: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onUpdateTools: (updates: McpToolUpdate[]) => void;
}

const formatTime = (value: string | null) => (value ? new Date(value).toLocaleString() : "never");

export function McpServerCard({
  server,
  syncing,
  updatingTools,
  onSync,
  onEdit,
  onDelete,
  onUpdateTools,
}: McpServerCardProps) {
  const [expanded, setExpanded] = useState(
    server.tools.length === 0 || server.tools.some((t) => !t.approved || t.changed),
  );
  const [showStatus, setShowStatus] = useState(false);

  const usable = server.tools.filter((t) => t.usable).length;
  const review = server.tools.filter((t) => !t.missing && (!t.approved || t.changed)).length;
  const isHttp = server.transport === "Http";

  const status = useQuery({
    queryKey: ["mcp-server-status", server.id],
    queryFn: () => fetchMcpServerStatus(server.id),
    enabled: showStatus,
    refetchInterval: showStatus ? 10_000 : false,
  });

  return (
    <li className={styles.server} data-testid={`mcp-server-${server.name}`}>
      <div className={styles.serverHead}>
        <span className={styles.serverIcon} aria-hidden="true">
          {isHttp ? <Globe size={16} /> : <Container size={16} />}
        </span>

        <div className={styles.serverMain}>
          <div className={styles.serverTitle}>
            <span className={styles.serverName}>{server.displayName}</span>
            <code className={styles.slug}>{server.name}</code>
            <Badge tone={isHttp ? "blue" : "purple"}>{isHttp ? "http" : "sandbox"}</Badge>
            {!server.enabled && <Badge tone="neutral">disabled</Badge>}
            {server.connection.connected && (
              <Badge tone="success" dot>
                connected
              </Badge>
            )}
          </div>
          <p className={styles.serverMeta}>
            <span className={styles.mono}>{isHttp ? server.endpoint : server.image}</span>
          </p>
          <p className={styles.serverMeta}>
            {usable} of {server.tools.length} tools available
            {review > 0 && <span className={styles.warn}> · {review} awaiting review</span>} · synced{" "}
            {formatTime(server.lastSyncedAt)}
          </p>
          {server.description && <p className={styles.serverDesc}>{server.description}</p>}
        </div>

        <div className={styles.serverActions}>
          <Button size="sm" variant="secondary" icon={<RefreshCw size={14} />} loading={syncing} onClick={onSync}>
            Sync
          </Button>
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            aria-label={`Status of ${server.displayName}`}
            icon={<Activity size={15} />}
            onClick={() => setShowStatus((s) => !s)}
          />
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            aria-label={`Edit ${server.displayName}`}
            icon={<Pencil size={15} />}
            onClick={onEdit}
          />
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            aria-label={`Delete ${server.displayName}`}
            icon={<Trash2 size={15} />}
            onClick={onDelete}
          />
          <Button
            size="sm"
            variant="ghost"
            iconOnly
            aria-label={expanded ? "Hide tools" : "Show tools"}
            aria-expanded={expanded}
            icon={<ChevronDown size={15} className={expanded ? styles.chevronOpen : styles.chevron} />}
            onClick={() => setExpanded((e) => !e)}
          />
        </div>
      </div>

      {(server.lastError ?? server.connection.lastError) && (
        <StatusMessage
          variant="error"
          className={styles.serverError}
          message={server.lastError ?? server.connection.lastError}
        />
      )}

      {showStatus && (
        <div className={styles.status}>
          {status.isLoading && <p className={styles.muted}>Loading status…</p>}
          {status.data && (
            <>
              <p className={styles.muted}>
                Connection: {status.data.connection.connected ? "open" : "closed"} · last used{" "}
                {formatTime(status.data.connection.lastUsedAt)}
              </p>
              {!isHttp && (
                <p className={styles.muted}>
                  Container:{" "}
                  {status.data.sandbox
                    ? `${status.data.sandbox.running ? "running" : "stopped"}${status.data.sandbox.containerId ? ` (${status.data.sandbox.containerId.slice(0, 12)})` : ""}`
                    : (status.data.sandboxError ?? "not started")}
                </p>
              )}
              {status.data.sandbox && status.data.sandbox.stderrTail.length > 0 && (
                <pre className={styles.stderr}>{status.data.sandbox.stderrTail.join("\n")}</pre>
              )}
            </>
          )}
        </div>
      )}

      {expanded && <McpToolsTable tools={server.tools} busy={updatingTools} onUpdate={onUpdateTools} />}
    </li>
  );
}
