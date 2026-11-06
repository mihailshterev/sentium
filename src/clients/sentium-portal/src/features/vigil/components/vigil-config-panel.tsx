import { useState } from "react";
import { Save, SlidersHorizontal } from "lucide-react";
import styles from "../vigil.module.css";
import { useVigilConfig } from "@/features/vigil/hooks/useVigilConfig";
import type { VigilConfig } from "@/features/vigil/types/serviceHealth";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Input } from "@/shared/ui/form-controls";
import { SkeletonText } from "@/shared/ui/skeleton";
import { StatusMessage } from "@/shared/ui/status-message";

const FIELDS: { key: keyof VigilConfig; label: string; hint: string; min: number; max: number }[] = [
  { key: "pollIntervalSeconds", label: "Poll Interval (s)", hint: "How often targets are probed", min: 5, max: 3600 },
  { key: "probeTimeoutSeconds", label: "Probe Timeout (s)", hint: "Per-probe timeout", min: 1, max: 60 },
  {
    key: "degradedLatencyMs",
    label: "Degraded Latency (ms)",
    hint: "Above this, a target is Degraded",
    min: 1,
    max: 60000,
  },
  {
    key: "consecutiveFailuresToOpenIncident",
    label: "Failures → Incident",
    hint: "Degraded cycles before an incident",
    min: 1,
    max: 100,
  },
  { key: "sampleHistorySize", label: "Sample History", hint: "Samples retained per target", min: 10, max: 200 },
];

export function VigilConfigPanel() {
  const { config, isLoading, saveConfig, isSaving, isSaveSuccess, isSaveError, saveError, resetSave } =
    useVigilConfig(true);
  const [draft, setDraft] = useState<VigilConfig | null>(null);
  const form = draft ?? config ?? null;

  const header = (
    <CardHeader
      icon={<SlidersHorizontal />}
      title="Monitoring Configuration"
      actions={<Badge tone="accent">Sovereign</Badge>}
    />
  );

  if (isLoading || !form) {
    return (
      <Card padding="md" as="section">
        {header}
        <div aria-busy="true">
          <span className="visually-hidden">Loading…</span>
          <SkeletonText lines={4} />
        </div>
      </Card>
    );
  }

  const onChange = (key: keyof VigilConfig, value: number) => {
    resetSave();
    setDraft({ ...form, [key]: value });
  };

  return (
    <Card padding="md" as="section">
      {header}
      <div className={styles.configGrid}>
        {FIELDS.map((field) => (
          <label key={field.key} className={styles.configField}>
            <span className={styles.configLabel}>{field.label}</span>
            <Input
              type="number"
              controlSize="sm"
              value={form[field.key]}
              min={field.min}
              max={field.max}
              onChange={(e) => onChange(field.key, Number(e.target.value))}
            />
            <span className={styles.configHint}>{field.hint}</span>
          </label>
        ))}
      </div>

      {isSaveError && <StatusMessage variant="error" message={(saveError as Error)?.message ?? "Failed to save"} />}

      <div className={styles.configFooter}>
        {isSaveSuccess && <span className={styles.saved}>Saved</span>}
        <Button
          variant="primary"
          size="sm"
          icon={<Save size={14} />}
          loading={isSaving}
          onClick={() => saveConfig(form)}
        >
          Save
        </Button>
      </div>
    </Card>
  );
}
