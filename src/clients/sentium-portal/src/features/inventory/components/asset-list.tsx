import { Link } from "react-router";
import { Package, Paperclip } from "lucide-react";
import styles from "../inventory.module.css";
import { Badge } from "@/shared/ui/badge";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadMore } from "@/shared/ui/load-more";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";
import type { AssetSummary } from "../types/inventory";
import { KnowledgeStatusBadge } from "./knowledge-status-badge";

interface AssetListProps {
  assets: AssetSummary[];
  isLoading: boolean;
  isError: boolean;
  hasMore: boolean;
  isLoadingMore: boolean;
  onLoadMore: () => void;
  hasFilters: boolean;
  emptyAction?: React.ReactNode;
}

export function AssetList({
  assets,
  isLoading,
  isError,
  hasMore,
  isLoadingMore,
  onLoadMore,
  hasFilters,
  emptyAction,
}: AssetListProps) {
  if (isLoading) {
    return (
      <div className={styles.listPadding} aria-busy="true">
        <SkeletonText lines={4} />
      </div>
    );
  }

  if (isError) {
    return (
      <div className={styles.listPadding}>
        <StatusMessage variant="error" message="Failed to load assets." />
      </div>
    );
  }

  if (assets.length === 0) {
    return hasFilters ? (
      <EmptyState icon={<Package />} title="No matching assets" hint="Try a different search or clear the filters." />
    ) : (
      <EmptyState
        icon={<Package />}
        title="Nothing catalogued here yet"
        hint="Add the first asset to this place."
        action={emptyAction}
      />
    );
  }

  return (
    <>
      <ul className={styles.assetList} aria-label="Assets">
        {assets.map((asset) => (
          <li key={asset.id}>
            <Link
              to={`/inventory/assets/${asset.id}`}
              className={styles.assetRow}
              data-testid={`asset-row-${asset.displayName}`}
            >
              <span className={styles.assetThumb} aria-hidden="true">
                {asset.coverImageUrl ? <img src={asset.coverImageUrl} alt="" loading="lazy" /> : <Package size={18} />}
              </span>
              <span className={styles.assetMain}>
                <span className={styles.assetName}>{asset.displayName}</span>
                <span className={styles.assetMeta}>
                  {[asset.category, [asset.manufacturer, asset.modelNumber].filter(Boolean).join(" ")]
                    .filter(Boolean)
                    .join(" · ") || "—"}
                </span>
                <span className={styles.assetPath}>{asset.locationPath}</span>
              </span>
              <span className={styles.assetSide}>
                {asset.tags.length > 0 && (
                  <span className={styles.assetTags}>
                    {asset.tags.slice(0, 3).map((t) => (
                      <Badge key={t} tone="neutral">
                        {t}
                      </Badge>
                    ))}
                    {asset.tags.length > 3 && <Badge tone="neutral">+{asset.tags.length - 3}</Badge>}
                  </span>
                )}
                <span className={styles.assetBadges}>
                  {asset.attachmentCount > 0 && (
                    <span className={styles.attachmentCount} title={`${asset.attachmentCount} attachment(s)`}>
                      <Paperclip size={12} aria-hidden="true" />
                      {asset.attachmentCount}
                    </span>
                  )}
                  <KnowledgeStatusBadge status={asset.knowledgeStatus} />
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      <LoadMore hasMore={hasMore} isLoading={isLoadingMore} onLoadMore={onLoadMore} />
    </>
  );
}
