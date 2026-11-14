import { Link } from "react-router";
import { Info, OctagonAlert, TriangleAlert, type LucideIcon } from "lucide-react";
import type { Notification, NotificationSeverity } from "@/features/notifications/types/notification";
import { formatRelativeTime } from "@/shared/utils/formatters";
import styles from "./notification-list.module.css";

const SEVERITY_ICON: Record<NotificationSeverity, LucideIcon> = {
  Info,
  Warning: TriangleAlert,
  Critical: OctagonAlert,
};

interface NotificationListProps {
  notifications: Notification[];
  /** Called when a notification is opened (followed or clicked); unread ones should be marked read. */
  onOpen: (notification: Notification) => void;
  compact?: boolean;
}

export function NotificationList({ notifications, onOpen, compact = false }: NotificationListProps) {
  return (
    <ul className={`${styles.list} ${compact ? styles.compact : ""}`}>
      {notifications.map((n) => (
        <li
          key={n.id}
          className={styles.item}
          data-severity={n.severity.toLowerCase()}
          data-unread={n.readAt ? undefined : true}
          data-testid="notification-item"
        >
          <NotificationEntry notification={n} onOpen={onOpen} />
        </li>
      ))}
    </ul>
  );
}

function NotificationEntry({ notification: n, onOpen }: { notification: Notification; onOpen: NotificationListProps["onOpen"] }) {
  const Icon = SEVERITY_ICON[n.severity] ?? Info;
  const content = (
    <>
      <Icon size={16} strokeWidth={1.75} className={styles.icon} aria-hidden="true" />
      <span className={styles.text}>
        <span className={styles.title}>{n.title}</span>
        {n.body && <span className={styles.body}>{n.body}</span>}
        <span className={styles.meta}>
          <span>{n.category}</span>
          <span aria-hidden="true">·</span>
          <time dateTime={n.updatedAt}>{formatRelativeTime(n.updatedAt)}</time>
          {n.occurrences > 1 && <span className={styles.repeats}>×{n.occurrences}</span>}
        </span>
      </span>
      {!n.readAt && <span className={styles.unreadDot} aria-label="unread" />}
    </>
  );

  if (n.link?.startsWith("/")) {
    return (
      <Link to={n.link} className={styles.entry} onClick={() => onOpen(n)}>
        {content}
      </Link>
    );
  }

  return (
    <button type="button" className={styles.entry} onClick={() => onOpen(n)}>
      {content}
    </button>
  );
}
