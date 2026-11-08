import { useState } from "react";
import { Plug, Plus, ShieldAlert } from "lucide-react";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import { McpServerCard } from "../components/mcp-server-card";
import { McpServerDialog } from "../components/mcp-server-dialog";
import { McpSettingsCard } from "../components/mcp-settings-card";
import { useMcpServers } from "../hooks/useMcpServers";
import type { McpServer } from "../types/mcp";
import { describeError } from "../utils/errors";
import styles from "../mcp.module.css";

export function McpServersView() {
  const { isSovereign } = useRole();

  return (
    <Page
      title="MCP Servers"
      icon={<Plug />}
      subtitle="Model Context Protocol servers on your local network whose tools agents can use"
    >
      {isSovereign ? (
        <McpServersAdmin />
      ) : (
        <EmptyState
          icon={<ShieldAlert />}
          title="Managed by a Sovereign"
          hint="MCP servers are registered and approved by a Sovereign. Approved tools appear in the agent tool picker."
        />
      )}
    </Page>
  );
}

function McpServersAdmin() {
  const mcp = useMcpServers();
  const [editing, setEditing] = useState<McpServer | null>(null);
  const [creating, setCreating] = useState(false);
  const [toDelete, setToDelete] = useState<McpServer | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const run = async (action: () => Promise<unknown>, fallback: string) => {
    setActionError(null);
    try {
      await action();
    } catch (err) {
      setActionError(describeError(err, fallback));
    }
  };

  return (
    <div className={styles.stack}>
      <McpSettingsCard />

      <Card as="section" padding="none">
        <CardHeader
          icon={<Plug />}
          title="Servers"
          description="Tools are opt-in: approve them here, then pick them per agent. 'All tools' never includes MCP tools."
          actions={
            <Button variant="primary" size="sm" icon={<Plus size={15} />} onClick={() => setCreating(true)}>
              Register server
            </Button>
          }
        />

        {actionError && <StatusMessage variant="error" className={styles.inlineMessage} message={actionError} />}

        {mcp.isLoading ? (
          <div className={styles.inlineMessage}>
            <SkeletonText lines={3} />
          </div>
        ) : mcp.servers.length === 0 ? (
          <EmptyState
            icon={<Plug />}
            title="No MCP servers yet"
            hint="Register a Streamable HTTP server on your network, or a stdio server the Sandbox runs in a container."
          />
        ) : (
          <ul className={styles.serverList}>
            {mcp.servers.map((server) => (
              <McpServerCard
                key={server.id}
                server={server}
                syncing={mcp.syncingId === server.id}
                updatingTools={mcp.updatingToolsId === server.id}
                onSync={() => void run(() => mcp.syncServer(server.id), "Sync failed.")}
                onEdit={() => setEditing(server)}
                onDelete={() => setToDelete(server)}
                onUpdateTools={(tools) =>
                  void run(() => mcp.updateTools({ id: server.id, tools }), "Could not update the tools.")
                }
              />
            ))}
          </ul>
        )}
      </Card>

      {(creating || editing) && (
        <McpServerDialog
          open
          server={editing}
          isSaving={mcp.isCreating || mcp.isUpdating}
          onCreate={mcp.createServer}
          onUpdate={(id, payload) => mcp.updateServer({ id, payload })}
          onClose={() => {
            setCreating(false);
            setEditing(null);
          }}
        />
      )}

      <ConfirmDialog
        open={toDelete !== null}
        variant="danger"
        title="Delete MCP server"
        description={`Delete "${toDelete?.displayName ?? ""}"? Its tools are removed from every agent that uses them.`}
        confirmLabel="Delete server"
        onConfirm={() => {
          const server = toDelete;
          setToDelete(null);
          if (server) {
            void run(() => mcp.deleteServer(server.id), "Could not delete the server.");
          }
        }}
        onCancel={() => setToDelete(null)}
      />
    </div>
  );
}
