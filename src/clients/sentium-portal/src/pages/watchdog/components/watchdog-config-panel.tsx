import { useState } from "react";
import { Save, SlidersHorizontal, Check, AlertCircle, Loader } from "lucide-react";
import styles from "../watchdog.module.scss";
import useWatchdogConfig from "../../../hooks/useWatchdogConfig";
import type { WatchdogConfig } from "../../../types/serviceHealth";

const FIELDS: { key: keyof WatchdogConfig; label: string; hint: string; min: number; max: number }[] = [
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

const WatchdogConfigPanel = () => {
  const { config, isLoading, saveConfig, isSaving, isSaveSuccess, isSaveError, saveError, resetSave } =
    useWatchdogConfig(true);
  const [draft, setDraft] = useState<WatchdogConfig | null>(null);
  const form = draft ?? config ?? null;

  if (isLoading || !form) {
    return (
      <section className={styles.card}>
        <div className={styles.cardHeader}>
          <span className={styles.cardTitle}>
            <SlidersHorizontal size={13} /> Monitoring Configuration
          </span>
        </div>
        <div className={styles.configLoading}>
          <Loader size={14} className={styles.spinning} /> Loading…
        </div>
      </section>
    );
  }

  const onChange = (key: keyof WatchdogConfig, value: number) => {
    resetSave();
    setDraft({ ...form, [key]: value });
  };

  return (
    <section className={styles.card}>
      <div className={styles.cardHeader}>
        <span className={styles.cardTitle}>
          <SlidersHorizontal size={13} /> Monitoring Configuration
        </span>
        <span className={styles.sovereignTag}>Sovereign</span>
      </div>

      <div className={styles.configGrid}>
        {FIELDS.map((field) => (
          <label key={field.key} className={styles.configField}>
            <span className={styles.configLabel}>{field.label}</span>
            <input
              type="number"
              className={styles.configInput}
              value={form[field.key]}
              min={field.min}
              max={field.max}
              onChange={(e) => onChange(field.key, Number(e.target.value))}
            />
            <span className={styles.configHint}>{field.hint}</span>
          </label>
        ))}
      </div>

      <div className={styles.configFooter}>
        {isSaveSuccess && (
          <span className={styles.configSuccess}>
            <Check size={13} /> Saved
          </span>
        )}
        {isSaveError && (
          <span className={styles.configError}>
            <AlertCircle size={13} /> {(saveError as Error)?.message ?? "Failed to save"}
          </span>
        )}
        <button className={styles.saveBtn} onClick={() => saveConfig(form)} disabled={isSaving}>
          {isSaving ? <Loader size={13} className={styles.spinning} /> : <Save size={13} />}
          Save
        </button>
      </div>
    </section>
  );
};

export default WatchdogConfigPanel;
