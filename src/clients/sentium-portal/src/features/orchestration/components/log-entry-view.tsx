import Markdown from "react-markdown";
import { AlertTriangle, Brain, CheckCircle2, ChevronDown, RefreshCw, Wrench, XCircle } from "lucide-react";
import type { ElementType } from "react";
import styles from "../agent-orchestration.module.css";
import type { LogEntry } from "@/features/orchestration/types/orchestration";
import { OrchestratorPlanView } from "./orchestrator-plan-view";
import { parseAssignments } from "../../../shared/utils/agent-helpers";

interface LogEntryViewProps {
  log: LogEntry;
  entryId: string;
  expanded: boolean;
  onToggle: (id: string) => void;
  getRoleClass: (author: string) => string;
  isActiveThought?: boolean;
}

const LogTypes = {
  THOUGHT: "thought",
  TOOL: "tool",
  MESSAGE: "message",
  STATUS: "status",
  ERROR: "error",
};

type Tone = "pass" | "fail" | "stuck" | "loop" | "thought" | "tool" | "message";

const statusMeta = (text: string): { tone: Tone; Icon: ElementType } => {
  const t = text.toLowerCase();
  if (t.includes("passed") || t.includes("validated")) {
    return { tone: "pass", Icon: CheckCircle2 };
  }
  if (t.includes("failed")) {
    return { tone: "fail", Icon: XCircle };
  }
  if (t.includes("stuck")) {
    return { tone: "stuck", Icon: AlertTriangle };
  }
  return { tone: "loop", Icon: RefreshCw };
};

const toneFor = (log: LogEntry): Tone => {
  switch (log.type) {
    case LogTypes.THOUGHT:
      return "thought";
    case LogTypes.TOOL:
      return "tool";
    case LogTypes.STATUS:
      return statusMeta(log.text).tone;
    case LogTypes.ERROR:
      return "fail";
    default:
      return "message";
  }
};

export function LogEntryView({
  log,
  entryId,
  expanded,
  onToggle,
  getRoleClass,
  isActiveThought = false,
}: LogEntryViewProps) {
  const author = <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>;

  const renderBody = () => {
    if (log.type === LogTypes.ERROR || log.type === LogTypes.STATUS) {
      const Icon = log.type === LogTypes.ERROR ? XCircle : statusMeta(log.text).Icon;
      return (
        <div className={styles.statusRow}>
          <Icon size={14} aria-hidden="true" />
          <span>{log.text}</span>
        </div>
      );
    }

    if (log.type === LogTypes.THOUGHT) {
      return (
        <>
          {author}
          <button
            type="button"
            className={`${styles.disclosure} ${isActiveThought ? styles.disclosureActive : ""}`}
            onClick={() => onToggle(entryId)}
            aria-expanded={expanded}
          >
            <Brain size={13} aria-hidden="true" />
            <span>Thinking</span>
            {isActiveThought && (
              <span className={styles.dots} aria-hidden="true">
                <span />
                <span />
                <span />
              </span>
            )}
            <ChevronDown size={13} className={expanded ? styles.chevronOpen : styles.chevron} />
          </button>
          {expanded && (
            <div className={`${styles.markdown} ${styles.thoughtContent}`}>
              <Markdown>{log.text}</Markdown>
            </div>
          )}
        </>
      );
    }

    if (log.type === LogTypes.TOOL) {
      return (
        <>
          {author}
          <div className={styles.toolCall}>
            <Wrench size={12} aria-hidden="true" />
            <span>{log.text}</span>
          </div>
        </>
      );
    }

    const planAssignments = log.author.toLowerCase().includes("orchestrator") ? parseAssignments(log.text) : null;

    return (
      <>
        {author}
        {planAssignments ? (
          <OrchestratorPlanView assignments={planAssignments} getRoleClass={getRoleClass} />
        ) : (
          <div className={styles.markdown}>
            <Markdown>{log.text}</Markdown>
          </div>
        )}
      </>
    );
  };

  return (
    <li className={styles.timelineItem} data-tone={toneFor(log)} data-kind={log.type}>
      <span className={styles.timelineDot} aria-hidden="true" />
      <div className={styles.timelineContent}>{renderBody()}</div>
    </li>
  );
}
