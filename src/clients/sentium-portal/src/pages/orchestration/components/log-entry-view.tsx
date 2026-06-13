import Markdown from "react-markdown";
import { Brain, ChevronDown, Wrench, CheckCircle2, XCircle, RefreshCw, AlertTriangle } from "lucide-react";
import type { ElementType } from "react";
import styles from "../agent-orchestration.module.scss";
import type { LogEntry } from "../../../types/orchestration";
import OrchestratorPlanView from "./orchestrator-plan-view";
import { parseAssignments } from "../../../utils/agent-helpers";

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
};

const statusMeta = (text: string): { dot: string; Icon: ElementType } => {
  const t = text.toLowerCase();
  if (t.includes("passed") || t.includes("validated")) {
    return { dot: styles.timelineDotPass, Icon: CheckCircle2 };
  }
  if (t.includes("failed")) {
    return { dot: styles.timelineDotFail, Icon: XCircle };
  }
  if (t.includes("stuck")) {
    return { dot: styles.timelineDotStuck, Icon: AlertTriangle };
  }
  return { dot: styles.timelineDotLoop, Icon: RefreshCw };
};

const dotClassFor = (log: LogEntry): string => {
  switch (log.type) {
    case LogTypes.THOUGHT:
      return styles.timelineDotThought;
    case LogTypes.TOOL:
      return styles.timelineDotTool;
    case LogTypes.STATUS:
      return statusMeta(log.text).dot;
    default:
      return styles.timelineDotMessage;
  }
};

const LogEntryView = ({
  log,
  entryId,
  expanded,
  onToggle,
  getRoleClass,
  isActiveThought = false,
}: LogEntryViewProps) => {
  const renderBody = () => {
    if (log.type === LogTypes.STATUS) {
      const { Icon } = statusMeta(log.text);
      return (
        <div className={styles.statusRow}>
          <Icon size={12} />
          <span>{log.text}</span>
        </div>
      );
    }

    if (log.type === LogTypes.THOUGHT) {
      return (
        <>
          <div className={styles.authorRow}>
            <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
            <div className={styles.authorLine} />
          </div>
          <div className={styles.thoughtBlock}>
            <button
              className={`${styles.thoughtHeader} ${isActiveThought ? styles.thoughtHeaderActive : ""}`}
              onClick={() => onToggle(entryId)}
            >
              <Brain size={11} className={isActiveThought ? styles.brainPulse : undefined} />
              <span>Thinking</span>
              {isActiveThought && (
                <span className={styles.thinkingDots}>
                  <span className={styles.thinkingDot} />
                  <span className={styles.thinkingDot} />
                  <span className={styles.thinkingDot} />
                </span>
              )}
              <ChevronDown
                size={11}
                className={`${styles.thoughtChevron} ${expanded ? styles.thoughtChevronOpen : ""}`}
              />
            </button>
            {expanded && (
              <div className={styles.thoughtContent}>
                <Markdown>{log.text}</Markdown>
              </div>
            )}
          </div>
        </>
      );
    }

    if (log.type === LogTypes.TOOL) {
      return (
        <>
          <div className={styles.authorRow}>
            <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
            <div className={styles.authorLine} />
          </div>
          <div className={styles.toolCallRow}>
            <Wrench size={10} />
            <span>{log.text}</span>
          </div>
        </>
      );
    }

    const planAssignments = log.author.toLowerCase().includes("orchestrator") ? parseAssignments(log.text) : null;

    return (
      <>
        <div className={styles.authorRow}>
          <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
          <div className={styles.authorLine} />
        </div>
        {planAssignments ? (
          <OrchestratorPlanView assignments={planAssignments} getRoleClass={getRoleClass} />
        ) : (
          <div className={styles.textContent}>
            <Markdown>{log.text}</Markdown>
          </div>
        )}
      </>
    );
  };

  return (
    <div className={`${styles.timelineItem} ${log.type === LogTypes.STATUS ? styles.timelineItemStatus : ""}`}>
      <div className={styles.timelineRail}>
        <span className={`${styles.timelineDot} ${dotClassFor(log)}`} />
        <span className={styles.timelineConnector} />
      </div>
      <div className={styles.timelineContent}>{renderBody()}</div>
    </div>
  );
};

export default LogEntryView;
