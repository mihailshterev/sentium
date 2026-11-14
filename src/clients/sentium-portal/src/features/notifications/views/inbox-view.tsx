import { useState } from "react";
import { Bell, CheckCheck, Inbox } from "lucide-react";
import { NotificationList } from "@/features/notifications/components/notification-list";
import { useInboxActions, useNotifications, useUnreadCount } from "@/features/notifications/hooks/useInbox";
import type { Notification } from "@/features/notifications/types/notification";
import { Button } from "@/shared/ui/button";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { Tabs } from "@/shared/ui/tabs";

const PAGE_LIMIT = 200;

type InboxFilter = "all" | "unread";

export function InboxView() {
  const [filter, setFilter] = useState<InboxFilter>("all");
  const unread = useUnreadCount();
  const { notifications, isLoading } = useNotifications(filter === "unread", PAGE_LIMIT);
  const { markRead, markAllRead, isMarkingAll } = useInboxActions();

  const open = (notification: Notification) => {
    if (!notification.readAt) {
      markRead(notification.id);
    }
  };

  return (
    <Page
      title="Inbox"
      subtitle="Alerts, suggestions and finished runs addressed to you"
      icon={<Bell size={20} />}
      actions={
        <Button icon={<CheckCheck size={15} />} onClick={() => markAllRead()} disabled={unread === 0} loading={isMarkingAll}>
          Mark all read
        </Button>
      }
    >
      <Tabs
        label="Inbox filter"
        active={filter}
        onChange={(id) => setFilter(id as InboxFilter)}
        tabs={[
          { id: "all", label: "All", testId: "inbox-tab-all" },
          { id: "unread", label: "Unread", count: unread, testId: "inbox-tab-unread" },
        ]}
      />

      <Card as="section" padding="none">
        {isLoading ? (
          <Skeleton height={160} />
        ) : notifications.length === 0 ? (
          <EmptyState
            icon={<Inbox size={24} />}
            title={filter === "unread" ? "You're all caught up" : "Nothing here yet"}
            hint="Old notifications are cleared automatically."
          />
        ) : (
          <NotificationList notifications={notifications} onOpen={open} />
        )}
      </Card>
    </Page>
  );
}
