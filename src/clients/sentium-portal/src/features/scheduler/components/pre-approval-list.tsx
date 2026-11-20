import { ShieldCheck } from "lucide-react";
import styles from "../scheduler.module.css";
import type { ToolDescriptor } from "@/features/agents/types/agents";
import { Badge } from "@/shared/ui/badge";

interface PreApprovalListProps {
  tools: ToolDescriptor[];
  selected: string[];
  onChange: (selected: string[]) => void;
  isLoading: boolean;
  /** Nothing to list yet because no agent or workflow is chosen. */
  hasTarget: boolean;
}

/**
 * The approval-gated tools the run could call. Nobody is there to approve during a scheduled run, so an unchecked
 * tool is refused and the agent reports what it could not do; a checked one runs, still checked by Sentinel.
 */
export function PreApprovalList({ tools, selected, onChange, isLoading, hasTarget }: PreApprovalListProps) {
  if (!hasTarget) {
    return <p className={styles.muted}>Choose an agent or workflow to see which of its tools need approval.</p>;
  }
  if (isLoading) {
    return <p className={styles.muted}>Checking which tools need approval…</p>;
  }
  if (tools.length === 0) {
    return (
      <p className={styles.muted}>
        <ShieldCheck size={14} aria-hidden="true" /> None of its tools need approval, so nothing has to be pre-approved.
      </p>
    );
  }

  const toggle = (name: string, on: boolean) =>
    onChange(on ? [...selected, name] : selected.filter((n) => n !== name));

  return (
    <div className={styles.approvalList} role="group" aria-label="Pre-approved tools">
      {tools.map((tool) => (
        <label key={tool.name} className={styles.approvalOption}>
          <input
            type="checkbox"
            checked={selected.includes(tool.name)}
            onChange={(e) => toggle(tool.name, e.target.checked)}
          />
          <span className={styles.approvalText}>
            <span className={styles.approvalName}>
              <code>{tool.name}</code>
              {tool.risk && <Badge tone={tool.risk === "High" ? "danger" : "warning"}>{tool.risk} risk</Badge>}
            </span>
            {tool.description && <span className={styles.approvalDescription}>{tool.description}</span>}
          </span>
        </label>
      ))}
    </div>
  );
}
