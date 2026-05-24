import Markdown from "react-markdown";
import { Brain, ChevronDown, Wrench } from "lucide-react";
import styles from "../agent-orchestration.module.scss";
import type { LogEntry } from "../../../types/orchestration";

interface LogEntryViewProps {
  log: LogEntry;
  entryId: string;
  expanded: boolean;
  onToggle: (id: string) => void;
  getRoleClass: (author: string) => string;
}

const LogTypes = {
  THOUGHT: "thought",
  TOOL: "tool",
  MESSAGE: "message",
};

const LogEntryView = ({ log, entryId, expanded, onToggle, getRoleClass }: LogEntryViewProps) => {
  if (log.type === LogTypes.THOUGHT) {
    return (
      <div className={styles.logEntry}>
        <div className={styles.authorRow}>
          <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
          <div className={styles.authorLine} />
        </div>
        <div className={styles.thoughtBlock}>
          <button className={styles.thoughtHeader} onClick={() => onToggle(entryId)}>
            <Brain size={11} />
            <span>Thinking</span>
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
      </div>
    );
  }

  if (log.type === LogTypes.TOOL) {
    return (
      <div className={styles.toolCallEntry}>
        <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
        <div className={styles.toolCallRow}>
          <Wrench size={10} />
          <span>{log.text}</span>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.logEntry}>
      <div className={styles.authorRow}>
        <span className={`${styles.roleBadge} ${styles[getRoleClass(log.author)]}`}>{log.author}</span>
        <div className={styles.authorLine} />
      </div>
      <div className={styles.textContent}>
        <Markdown>{log.text}</Markdown>
      </div>
    </div>
  );
};

export default LogEntryView;
