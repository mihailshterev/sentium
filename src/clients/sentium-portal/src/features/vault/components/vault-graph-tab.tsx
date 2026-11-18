import { useState } from "react";
import { Network } from "lucide-react";
import { Card } from "@/shared/ui/card";
import { EmptyState } from "@/shared/ui/empty-state";
import { Select } from "@/shared/ui/form-controls";
import { Skeleton } from "@/shared/ui/skeleton";
import { useVaultGraph, useVaultTags } from "@/features/vault/hooks/useVault";
import { VaultGraphCanvas } from "./vault-graph-canvas";
import styles from "../vault.module.css";

interface VaultGraphTabProps {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

/** Loaded on demand (d3 is one of the largest libraries in the app). */
export default function VaultGraphTab({ selectedId, onSelect }: VaultGraphTabProps) {
  const [tag, setTag] = useState("");
  const { data: tags = [] } = useVaultTags(true);
  const { data: graph, isLoading } = useVaultGraph(tag || undefined, true);

  return (
    <Card className={styles.graphPanel}>
      <div className={styles.graphToolbar}>
        <Select controlSize="sm" aria-label="Graph tag" value={tag} onChange={(e) => setTag(e.target.value)}>
          <option value="">Whole vault</option>
          {tags.map((t) => (
            <option key={t.tag} value={t.tag}>
              #{t.tag} ({t.count})
            </option>
          ))}
        </Select>
        {graph && (
          <span className={styles.muted}>
            {graph.nodes.length} notes · {graph.edges.length} links
            {graph.truncated && " · best-linked notes only"}
          </span>
        )}
      </div>

      {isLoading && <Skeleton height="100%" className={styles.graphSkeleton} />}

      {graph && graph.nodes.length === 0 && (
        <EmptyState icon={<Network />} title="Nothing to draw" hint="No notes match this tag." />
      )}

      {graph && graph.nodes.length > 0 && <VaultGraphCanvas graph={graph} selectedId={selectedId} onSelect={onSelect} />}
    </Card>
  );
}
