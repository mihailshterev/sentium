import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router";
import { Bell, CheckCheck, Inbox } from "lucide-react";
import { useInboxActions, useNotifications, useUnreadCount } from "@/features/notifications/hooks/useInbox";
import { useNotificationStream } from "@/features/notifications/hooks/useNotificationStream";
import type { Notification } from "@/features/notifications/types/notification";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { Tooltip } from "@/shared/ui/tooltip";
import { NotificationList } from "./notification-list";
import styles from "./notification-bell.module.css";

const PANEL_LIMIT = 20;
const PANEL_GAP = 8;

interface NotificationBellProps {
  tooltipSide?: "top" | "right";
}

/**
 * Bell with the unread count; opens the Inbox panel. Mounting it also keeps the inbox live through the SSE stream.
 * The panel is portalled to the body because the sidebar clips its overflow.
 */
export function NotificationBell({ tooltipSide = "top" }: NotificationBellProps) {
  useNotificationStream();
  const unread = useUnreadCount();
  const [panelPosition, setPanelPosition] = useState<React.CSSProperties | null>(null);
  const anchorRef = useRef<HTMLButtonElement>(null);
  const open = panelPosition !== null;

  const toggle = () => {
    if (open) {
      setPanelPosition(null);
      return;
    }

    const rect = anchorRef.current?.getBoundingClientRect();
    setPanelPosition(
      rect ? { left: rect.right + PANEL_GAP, bottom: Math.max(PANEL_GAP, window.innerHeight - rect.bottom) } : {},
    );
  };

  const label = unread > 0 ? `Notifications, ${unread} unread` : "Notifications";

  return (
    <span className={styles.anchor}>
      <Tooltip content="Notifications" side={tooltipSide} disabled={open}>
        <Button
          ref={anchorRef}
          variant="ghost"
          size="sm"
          iconOnly
          aria-label={label}
          aria-haspopup="dialog"
          aria-expanded={open}
          data-testid="notification-bell"
          onClick={toggle}
          icon={<Bell size={16} />}
        />
      </Tooltip>
      {unread > 0 && (
        <span className={styles.badge} data-testid="notification-badge" aria-hidden="true">
          {unread > 99 ? "99+" : unread}
        </span>
      )}
      {panelPosition && (
        <InboxPanel anchor={anchorRef} position={panelPosition} onClose={() => setPanelPosition(null)} />
      )}
    </span>
  );
}

interface InboxPanelProps {
  anchor: React.RefObject<HTMLButtonElement | null>;
  position: React.CSSProperties;
  onClose: () => void;
}

function InboxPanel({ anchor, position, onClose }: InboxPanelProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const { notifications, isLoading } = useNotifications(false, PANEL_LIMIT);
  const { markRead, markAllRead, isMarkingAll } = useInboxActions();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
        anchor.current?.focus();
      }
    };
    const onPointer = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!panelRef.current?.contains(target) && !anchor.current?.contains(target)) {
        onClose();
      }
    };

    window.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer);
    return () => {
      window.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointer);
    };
  }, [anchor, onClose]);

  const open = (notification: Notification) => {
    if (!notification.readAt) {
      markRead(notification.id);
    }

    if (notification.link) {
      onClose();
    }
  };

  const hasUnread = notifications.some((n) => !n.readAt);

  return createPortal(
    <div ref={panelRef} role="dialog" aria-label="Inbox" className={`frame ${styles.panel}`} style={position}>
      <header className={styles.header}>
        <span className={styles.heading}>inbox</span>
        <Button
          variant="ghost"
          size="sm"
          icon={<CheckCheck size={14} />}
          onClick={() => markAllRead()}
          disabled={!hasUnread}
          loading={isMarkingAll}
        >
          Mark all read
        </Button>
      </header>

      <div className={styles.body}>
        {!isLoading && notifications.length === 0 ? (
          <EmptyState
            compact
            icon={<Inbox size={20} />}
            title="Nothing here yet"
            hint="Alerts, suggestions and finished runs show up here."
          />
        ) : (
          <NotificationList notifications={notifications} onOpen={open} compact />
        )}
      </div>

      <footer className={styles.footer}>
        <Link to="/inbox" className={styles.footerLink} onClick={onClose}>
          Open inbox
        </Link>
      </footer>
    </div>,
    document.body,
  );
}
