import { Link } from "react-router";
import { ArrowUpRight } from "lucide-react";
import styles from "../sentinel.module.css";
import type { PdpSettings } from "@/features/sentinel/types/sentinel";

interface SovereignControlsProps {
  settings: PdpSettings | undefined;
  isUpdating: boolean;
  onLockdown: () => void;
}

const autonomyName = (level: number) =>
  level <= 2 ? "Max security" : level >= 9 ? "Max autonomy" : `Level ${level}`;

/**
 * Lockdown stays here as an immediate emergency switch; the rest of the policy is configured in Settings and only
 * summarized.
 */
export function SovereignControls({ settings, isUpdating, onLockdown }: SovereignControlsProps) {
  const policy = settings
    ? [
        { label: "Autonomy", value: autonomyName(settings.autonomyLevel) },
        { label: "Intent check", value: settings.semanticIntentCheckEnabled ? "On" : "Off" },
        { label: "Intent model", value: settings.intentCheckModel || "Default model" },
        { label: "Rate limit", value: `${settings.rateLimitMaxRequests} req / ${settings.rateLimitWindowSeconds}s` },
      ]
    : [];

  return (
    <div className={styles.controls}>
      <div className={styles.controlRow}>
        <div className={styles.controlInfo}>
          <span className={styles.controlLabel}>Lockdown Mode</span>
          <span className={styles.controlDesc}>Deny all non-Read agent actions immediately.</span>
        </div>
        <button
          type="button"
          className={styles.switch}
          data-tone="danger"
          onClick={onLockdown}
          disabled={isUpdating || !settings}
          aria-pressed={settings?.lockdownMode}
          aria-label="Lockdown Mode"
          data-testid="lockdown-toggle"
        >
          <span className={styles.switchThumb} />
        </button>
      </div>

      <dl className={styles.policy}>
        {policy.map((item) => (
          <div key={item.label} className={styles.policyRow}>
            <dt>{item.label}</dt>
            <dd>{item.value}</dd>
          </div>
        ))}
      </dl>

      <Link to="/settings/security" className={styles.configureLink}>
        Configure policy in Settings
        <ArrowUpRight size={13} aria-hidden="true" />
      </Link>
    </div>
  );
}
