import { Suspense, lazy, useCallback, useState } from "react";
import { useSearchParams } from "react-router";
import { FileText, Library, Link2, Network, RefreshCw, Trash2, Unlink } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ConfirmDialog } from "@/shared/ui/confirm-dialog";
import { EmptyState } from "@/shared/ui/empty-state";
import { Page } from "@/shared/ui/page";
import { Skeleton } from "@/shared/ui/skeleton";
import { StatCard, StatGrid } from "@/shared/ui/stat-card";
import { Tabs } from "@/shared/ui/tabs";
import { formatRelativeTime } from "@/shared/utils/formatters";
import { useForgetVault, useVaultSummary } from "@/features/vault/hooks/useVault";
import type { VaultSummary } from "@/features/vault/types/vault";
import { VaultNoteList } from "../components/vault-note-list";
import { VaultNotePanel } from "../components/vault-note-panel";
import styles from "../vault.module.css";

const VaultGraphTab = lazy(() => import("../components/vault-graph-tab"));

type Tab = "notes" | "graph";

const TABS = [
  { id: "notes", label: "Notes", icon: <FileText />, testId: "vault-tab-notes" },
  { id: "graph", label: "Graph", icon: <Network />, testId: "vault-tab-graph" },
];

const NOTE_PARAM = "note";

/** The user's Obsidian vault as Sentium sees it: synced one-way by Sentium Satelles, never written to. */
export function VaultView() {
  const { data: summary, isLoading } = useVaultSummary();
  const [searchParams, setSearchParams] = useSearchParams();
  const [tab, setTab] = useState<Tab>("notes");
  const [confirmForget, setConfirmForget] = useState(false);
  const forget = useForgetVault();

  const selectedId = searchParams.get(NOTE_PARAM);
  const selectNote = useCallback(
    (id: string) => {
      setSearchParams((params) => {
        params.set(NOTE_PARAM, id);
        return params;
      });
      setTab("notes");
    },
    [setSearchParams],
  );

  return (
    <Page
      title="Vault"
      subtitle="Your Obsidian notes, linked and searchable by your assistants"
      width="wide"
      actions={
        summary && (
          <Button icon={<Trash2 />} onClick={() => setConfirmForget(true)} loading={forget.isPending}>
            Forget vault
          </Button>
        )
      }
    >
      {isLoading && <Skeleton height={96} />}

      {!isLoading && !summary && (
        <EmptyState
          icon={<Library />}
          title="No vault synced yet"
          hint="Install Sentium Satelles on the PC that holds your Obsidian vault, pair it from your profile, then choose the vault folder in Satelles. Sentium only reads your notes; it never changes them."
        />
      )}

      {summary && (
        <>
          <VaultStats summary={summary} />
          <Tabs tabs={TABS} active={tab} onChange={(id) => setTab(id as Tab)} label="Vault views" />

          {tab === "notes" && (
            <div className={styles.split}>
              <VaultNoteList selectedId={selectedId} onSelect={selectNote} />
              <VaultNotePanel noteId={selectedId} onOpenNote={selectNote} />
            </div>
          )}

          {tab === "graph" && (
            <Suspense fallback={<Skeleton height={480} />}>
              <VaultGraphTab selectedId={selectedId} onSelect={selectNote} />
            </Suspense>
          )}
        </>
      )}

      <ConfirmDialog
        open={confirmForget}
        title="Forget this vault?"
        description="Sentium deletes its copy of every note and removes them from the knowledge base. Your notes in Obsidian are not touched. Satelles uploads the vault again on its next sync unless you remove it there."
        confirmLabel="Forget vault"
        onConfirm={() => {
          setConfirmForget(false);
          forget.mutate();
        }}
        onCancel={() => setConfirmForget(false)}
      />
    </Page>
  );
}

function VaultStats({ summary }: { summary: VaultSummary }) {
  const indexing = summary.pendingIndexCount > 0;

  return (
    <StatGrid columns={4}>
      <StatCard
        icon={<Library />}
        iconColor="cyan"
        label={summary.name}
        value={summary.noteCount}
        hint={
          summary.lastSyncAt
            ? `synced ${formatRelativeTime(summary.lastSyncAt)}${summary.deviceName ? ` from ${summary.deviceName}` : ""}`
            : "waiting for the first sync"
        }
      />
      <StatCard icon={<Link2 />} iconColor="purple" label="links" value={summary.linkCount} hint={`${summary.orphanCount} orphan notes`} />
      <StatCard icon={<Unlink />} iconColor="amber" label="unresolved" value={summary.unresolvedLinkCount} hint="links to notes not created yet" />
      <StatCard
        icon={<RefreshCw />}
        iconColor={summary.failedIndexCount > 0 ? "red" : "green"}
        label="indexing"
        value={summary.pendingIndexCount}
        chip={summary.failedIndexCount > 0 ? `${summary.failedIndexCount} failed` : indexing ? "running" : "up to date"}
        chipVariant={summary.failedIndexCount > 0 ? "red" : indexing ? "amber" : "green"}
      />
    </StatGrid>
  );
}
