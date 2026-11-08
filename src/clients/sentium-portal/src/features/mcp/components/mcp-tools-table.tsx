import { ShieldCheck } from "lucide-react";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Switch } from "@/shared/ui/form-controls";
import type { McpTool, McpToolUpdate } from "../types/mcp";
import styles from "../mcp.module.css";

interface McpToolsTableProps {
  tools: McpTool[];
  busy: boolean;
  onUpdate: (updates: McpToolUpdate[]) => void;
}

function toolState(tool: McpTool): { label: string; tone: BadgeTone } {
  if (tool.missing) return { label: "missing", tone: "neutral" };
  if (tool.changed) return { label: "changed - re-approve", tone: "danger" };
  if (!tool.approved) return { label: "needs approval", tone: "warning" };
  if (!tool.enabled) return { label: "disabled", tone: "neutral" };
  return { label: "available", tone: "success" };
}

export function McpToolsTable({ tools, busy, onUpdate }: McpToolsTableProps) {
  const pending = tools.filter((t) => !t.missing && (!t.approved || t.changed));

  if (tools.length === 0) {
    return <p className={styles.muted}>No tools discovered yet - run a sync.</p>;
  }

  return (
    <div className={styles.tools}>
      {pending.length > 1 && (
        <div className={styles.toolsBar}>
          <span className={styles.muted}>{pending.length} tools need review.</span>
          <Button
            size="sm"
            variant="secondary"
            icon={<ShieldCheck size={14} />}
            disabled={busy}
            onClick={() => onUpdate(pending.map((t) => ({ name: t.name, approve: true })))}
          >
            Approve all after review
          </Button>
        </div>
      )}

      <ul className={styles.toolList}>
        {tools.map((tool) => {
          const state = toolState(tool);
          return (
            <li key={tool.name} className={styles.toolRow} data-testid={`mcp-tool-${tool.name}`}>
              <div className={styles.toolMain}>
                <div className={styles.toolTitle}>
                  <code className={styles.toolName}>{tool.qualifiedName}</code>
                  <Badge tone={state.tone}>{state.label}</Badge>
                  {tool.readOnlyHint && <Badge tone="info">read-only</Badge>}
                  {tool.destructiveHint && <Badge tone="red">destructive</Badge>}
                  {tool.openWorldHint && <Badge tone="amber">open world</Badge>}
                </div>
                <p className={styles.toolDesc}>{tool.description ?? tool.title ?? "No description."}</p>
                {tool.changed && tool.approvedDescription !== tool.description && (
                  <p className={styles.toolDiff}>
                    <span className={styles.toolDiffLabel}>was:</span> {tool.approvedDescription ?? "(no description)"}
                  </p>
                )}
                <details className={styles.schema}>
                  <summary>input schema</summary>
                  <pre>{formatSchema(tool.inputSchema)}</pre>
                </details>
              </div>

              <div className={styles.toolControls}>
                {(!tool.approved || tool.changed) && !tool.missing && (
                  <Button
                    size="sm"
                    variant="primary"
                    disabled={busy}
                    onClick={() => onUpdate([{ name: tool.name, approve: true, enabled: true }])}
                  >
                    Approve
                  </Button>
                )}
                <label className={styles.toggle}>
                  <Switch
                    checked={tool.enabled}
                    disabled={busy}
                    aria-label={`Enable ${tool.qualifiedName}`}
                    onChange={(e) => onUpdate([{ name: tool.name, enabled: e.target.checked }])}
                  />
                  <span>enabled</span>
                </label>
                <label className={styles.toggle}>
                  <Switch
                    checked={tool.requiresApproval}
                    disabled={busy}
                    aria-label={`Ask before each call to ${tool.qualifiedName}`}
                    onChange={(e) => onUpdate([{ name: tool.name, requiresApproval: e.target.checked }])}
                  />
                  <span title="Unattended workflows and schedules only get tools that do not ask">ask each call</span>
                </label>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function formatSchema(schema: string) {
  try {
    return JSON.stringify(JSON.parse(schema), null, 2);
  } catch {
    return schema;
  }
}
