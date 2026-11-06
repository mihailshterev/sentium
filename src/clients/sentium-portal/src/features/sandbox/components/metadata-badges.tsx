import { AlertTriangle, Clock, Cpu, Hash, MemoryStick } from "lucide-react";
import styles from "../sandbox.module.css";
import { Badge } from "@/shared/ui/badge";
import type { SandboxExecutionLog } from "@/features/sandbox/types/sandbox";
import { RunStatusBadge } from "./run-status-badge";

interface MetadataBadgesProps {
  entry: SandboxExecutionLog;
}

export function MetadataBadges({ entry }: MetadataBadgesProps) {
  return (
    <div className={styles.badges}>
      {entry.policyDenied ? <Badge tone="warning">Policy Denied</Badge> : <RunStatusBadge entry={entry} />}
      {entry.timedOut && (
        <Badge tone="warning">
          <AlertTriangle size={12} aria-hidden="true" /> Timed Out
        </Badge>
      )}
      <Badge tone="neutral">
        <Hash size={12} aria-hidden="true" /> exit {entry.exitCode}
      </Badge>
      <Badge tone="neutral">
        <Clock size={12} aria-hidden="true" /> {entry.durationMs.toLocaleString()} ms
      </Badge>
      <Badge tone="neutral">
        <MemoryStick size={12} aria-hidden="true" /> 256 MB
      </Badge>
      <Badge tone="neutral">
        <Cpu size={12} aria-hidden="true" /> 0.5 vCPU
      </Badge>
    </div>
  );
}
