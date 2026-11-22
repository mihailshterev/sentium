import { Link } from "react-router";
import { ArrowRight, CheckCircle2 } from "lucide-react";
import styles from "../dashboard.module.css";
import type { AttentionItem } from "../hooks/useAttention";
import { Badge } from "@/shared/ui/badge";
import { Card, CardHeader } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Skeleton } from "@/shared/ui/skeleton";
import { formatRelativeTime } from "@/shared/utils/formatters";

const VISIBLE_ITEMS = 6;

interface AttentionPanelProps {
  items: AttentionItem[];
  loading: boolean;
}

export function AttentionPanel({ items, loading }: AttentionPanelProps) {
  const hidden = items.length - VISIBLE_ITEMS;

  return (
    <Card padding="none" as="section" aria-label="Needs attention">
      <CardHeader
        title="Needs attention"
        actions={items.length > 0 && <Badge tone={items.some((i) => i.tone === "critical") ? "danger" : "warning"}>{items.length}</Badge>}
      />

      {loading ? (
        <div className={styles.listSkeleton} aria-busy="true">
          <Skeleton height={40} />
          <Skeleton height={40} />
          <Skeleton height={40} />
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          compact
          icon={<CheckCircle2 />}
          title="All clear"
          hint="No failing services, open incidents, risky runs or unread alerts."
        />
      ) : (
        <ul className={styles.rowList}>
          {items.slice(0, VISIBLE_ITEMS).map((item) => (
            <li key={item.id}>
              <Link
                to={item.to}
                className={styles.attentionRow}
                aria-label={`${item.tone === "critical" ? "Critical" : "Warning"}: ${item.title}${item.detail ? `. ${item.detail}` : ""}`}
              >
                <span className={styles.toneBar} data-tone={item.tone} aria-hidden="true" />
                <span className={styles.rowBody}>
                  <span className={styles.rowTitle}>{item.title}</span>
                  {item.detail && <span className={styles.rowDetail}>{item.detail}</span>}
                </span>
                <span className={styles.rowMeta}>
                  {item.source}
                  {item.at && ` · ${formatRelativeTime(item.at)}`}
                </span>
                <ArrowRight size={14} className={styles.rowArrow} aria-hidden="true" />
              </Link>
            </li>
          ))}
        </ul>
      )}

      {hidden > 0 && (
        <Link to="/inbox" className={styles.panelFooterLink}>
          {hidden} more in the inbox <ArrowRight size={13} aria-hidden="true" />
        </Link>
      )}
    </Card>
  );
}
