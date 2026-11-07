import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ArrowLeft, Bot, ChevronRight, Package, Pencil, Trash2 } from "lucide-react";
import styles from "../inventory.module.css";
import { ApiError } from "@/shared/api/client";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { formatDateTimeShort } from "@/shared/utils/formatters";
import { AssetFormDialog } from "../components/asset-form-dialog";
import { AttachmentsPanel } from "../components/attachments-panel";
import { KnowledgeStatusBadge } from "../components/knowledge-status-badge";
import { useAsset, useAssetMutations, useAttachmentMutations, useLocations, useTaxonomy } from "../hooks/useInventory";
import type { Asset } from "../types/inventory";

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : error ? "Something went wrong." : null;

function DetailRow({ label, value }: { label: string; value: React.ReactNode }) {
  if (value == null || value === "") return null;
  return (
    <div className={styles.detailRow}>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function Breadcrumb({ asset }: { asset: Asset }) {
  return (
    <span role="navigation" aria-label="Location" className={styles.breadcrumb}>
      {asset.locationCrumbs.map((crumb, i) => (
        <span key={crumb.id} className={styles.crumb}>
          {i > 0 && <ChevronRight size={12} aria-hidden="true" />}
          <Link to={`/inventory?location=${crumb.id}`}>{crumb.name}</Link>
        </span>
      ))}
    </span>
  );
}

export function AssetDetailView() {
  const { assetId } = useParams<{ assetId: string }>();
  const navigate = useNavigate();
  const { asset, isLoading, error } = useAsset(assetId);
  const { locations } = useLocations();
  const { tags, categories } = useTaxonomy();
  const mutations = useAssetMutations();
  const attachments = useAttachmentMutations(assetId ?? "");

  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const back = () => navigate(asset ? `/inventory?location=${asset.locationId}` : "/inventory");

  if (isLoading) {
    return (
      <Page title="Asset" width="wide">
        <SkeletonText lines={6} />
      </Page>
    );
  }

  if (!asset) {
    return (
      <Page title="Asset" width="wide">
        <EmptyState
          icon={<Package />}
          title={
            error instanceof ApiError && error.isNotFound ? "This asset no longer exists" : "Could not load the asset"
          }
          action={
            <Button variant="secondary" onClick={() => navigate("/inventory")}>
              Back to inventory
            </Button>
          }
        />
      </Page>
    );
  }

  return (
    <Page
      title={asset.displayName}
      subtitle={<Breadcrumb asset={asset} />}
      width="wide"
      leading={
        <Button
          variant="secondary"
          size="sm"
          iconOnly
          icon={<ArrowLeft size={16} />}
          onClick={back}
          title="Back to inventory"
        />
      }
      actions={
        <>
          <Button
            variant="secondary"
            icon={<Pencil size={15} />}
            onClick={() => {
              mutations.update.reset();
              setEditing(true);
            }}
          >
            Edit
          </Button>
          <Button
            variant="danger"
            icon={<Trash2 size={15} />}
            onClick={() => setConfirmDelete(true)}
            data-testid={`asset-delete-${asset.displayName}`}
          >
            Delete
          </Button>
        </>
      }
    >
      <div className={styles.detailLayout}>
        <div className={styles.detailColumn}>
          <Card padding="md" as="section">
            <CardHeader icon={<Package />} title="Details" />
            <dl className={styles.detailList}>
              <DetailRow label="Category" value={asset.category} />
              <DetailRow
                label="Tags"
                value={
                  asset.tags.length > 0 ? (
                    <span className={styles.assetTags}>
                      {asset.tags.map((t) => (
                        <Link key={t} to={`/inventory?tag=${encodeURIComponent(t)}`}>
                          <Badge tone="neutral">{t}</Badge>
                        </Link>
                      ))}
                    </span>
                  ) : null
                }
              />
              <DetailRow label="Manufacturer" value={asset.manufacturer} />
              <DetailRow label="Model" value={asset.modelNumber} />
              <DetailRow label="Serial number" value={asset.serialNumber} />
              <DetailRow label="Purchased" value={asset.purchaseDate} />
              <DetailRow label="Last serviced" value={asset.lastServicedDate} />
              <DetailRow label="Warranty" value={asset.warrantyInfo} />
              <DetailRow label="Description" value={asset.physicalDescription} />
              <DetailRow label="Usage and care" value={asset.instructions} />
              <DetailRow
                label="Updated"
                value={`${formatDateTimeShort(asset.updatedAt)}${asset.updatedBy ? ` by ${asset.updatedBy}` : ""}`}
              />
            </dl>
          </Card>

          <Card padding="md" as="section">
            <CardHeader
              icon={<Bot />}
              title="Assistants"
              actions={
                <KnowledgeStatusBadge
                  status={asset.knowledgeStatus}
                  error={asset.knowledgeError}
                  onRetry={() => mutations.retrySync.mutate(asset.id)}
                  isRetrying={mutations.retrySync.isPending}
                />
              }
            />
            {asset.isAgentAccessible ? (
              <>
                <p className={styles.muted}>
                  Assistants can find this item, tell people where it is and answer questions about it. Attachments and
                  location access notes are never shared.
                </p>
                {asset.agentInstructions && (
                  <blockquote className={styles.agentNotes}>{asset.agentInstructions}</blockquote>
                )}
                {asset.knowledgeStatus === "Failed" && asset.knowledgeError && (
                  <p className={styles.errorText}>Last error: {asset.knowledgeError}</p>
                )}
              </>
            ) : (
              <p className={styles.muted}>
                This item is private. Edit it and turn on “Share with assistants” to change that.
              </p>
            )}
          </Card>
        </div>

        <AttachmentsPanel
          attachments={asset.attachments}
          isUploading={attachments.upload.isPending}
          uploadError={errorMessage(attachments.upload.error)}
          onUpload={(file, kind) => attachments.upload.mutate({ file, kind })}
          onDelete={(attachment) => attachments.remove.mutate(attachment.id)}
        />
      </div>

      {editing && (
        <AssetFormDialog
          locations={locations}
          asset={asset}
          categories={categories}
          tagSuggestions={tags.map((t) => t.name)}
          isPending={mutations.update.isPending}
          error={errorMessage(mutations.update.error)}
          onCancel={() => setEditing(false)}
          onSubmit={(payload) =>
            mutations.update.mutate({ id: asset.id, payload }, { onSuccess: () => setEditing(false) })
          }
        />
      )}

      <ConfirmDialog
        open={confirmDelete}
        title={`Delete ${asset.displayName}?`}
        description="The asset, its attachments and everything assistants know about it will be removed. This cannot be undone."
        confirmLabel="Delete asset"
        variant="danger"
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() =>
          mutations.remove.mutate(asset.id, {
            onSuccess: () => navigate(`/inventory?location=${asset.locationId}`),
          })
        }
      />
    </Page>
  );
}
