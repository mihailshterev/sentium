import styles from "../sentinel.module.css";
import { Range } from "@/shared/ui/form-controls";
import { ModelSelector } from "@/shared/ui/model-selector";
import type { PdpSettings } from "@/features/sentinel/types/sentinel";

interface SovereignControlsProps {
  settings: PdpSettings | undefined;
  isUpdating: boolean;
  displayAutonomy: number;
  models: string[];
  onLockdown: () => void;
  onSemanticToggle: () => void;
  onAutonomyChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  commitAutonomy: () => void;
  onIntentModelChange: (model: string) => void;
}

interface ToggleProps {
  label: string;
  description: string;
  pressed: boolean | undefined;
  disabled: boolean;
  onToggle: () => void;
  testId: string;
  tone?: "danger";
}

/** Toggle button (aria-pressed) drawn as a switch. */
function ToggleRow({ label, description, pressed, disabled, onToggle, testId, tone }: ToggleProps) {
  return (
    <div className={styles.controlRow}>
      <div className={styles.controlInfo}>
        <span className={styles.controlLabel}>{label}</span>
        <span className={styles.controlDesc}>{description}</span>
      </div>
      <button
        type="button"
        className={styles.switch}
        data-tone={tone}
        onClick={onToggle}
        disabled={disabled}
        aria-pressed={pressed}
        aria-label={label}
        data-testid={testId}
      >
        <span className={styles.switchThumb} />
      </button>
    </div>
  );
}

export function SovereignControls({
  settings,
  isUpdating,
  displayAutonomy,
  models,
  onLockdown,
  onSemanticToggle,
  onAutonomyChange,
  commitAutonomy,
  onIntentModelChange,
}: SovereignControlsProps) {
  const disabled = isUpdating || !settings;
  const autonomyLabel =
    displayAutonomy <= 2 ? "Max Security" : displayAutonomy >= 9 ? "Max Autonomy" : `Level ${displayAutonomy}`;

  return (
    <div className={styles.controls}>
      <ToggleRow
        label="Lockdown Mode"
        description="Deny all non-Read agent actions immediately."
        pressed={settings?.lockdownMode}
        disabled={disabled}
        onToggle={onLockdown}
        testId="lockdown-toggle"
        tone="danger"
      />

      <ToggleRow
        label="Semantic Intent Check"
        description="LLM verification of agent intent alignment."
        pressed={settings?.semanticIntentCheckEnabled}
        disabled={disabled}
        onToggle={onSemanticToggle}
        testId="semantic-intent-toggle"
      />

      <div className={styles.controlRow}>
        <div className={styles.controlInfo}>
          <span className={styles.controlLabel}>Intent Check Model</span>
          <span className={styles.controlDesc}>Model used for semantic intent verification.</span>
        </div>
        <ModelSelector
          models={models}
          value={settings?.intentCheckModel ?? ""}
          onChange={onIntentModelChange}
          disabled={disabled}
          variant="chip"
        />
      </div>

      <div className={styles.slider}>
        <div className={styles.sliderHeader}>
          <label className={styles.controlLabel} htmlFor="autonomy-slider">
            AI Autonomy
          </label>
          <span className={styles.sliderValue}>{autonomyLabel}</span>
        </div>
        <Range
          id="autonomy-slider"
          min={1}
          max={10}
          step={1}
          value={displayAutonomy}
          onChange={onAutonomyChange}
          onMouseUp={commitAutonomy}
          onTouchEnd={commitAutonomy}
          onKeyUp={commitAutonomy}
          disabled={disabled}
          data-testid="autonomy-slider"
        />
        <div className={styles.sliderScale}>
          <span>High Security</span>
          <span>High Autonomy</span>
        </div>
        <p className={styles.controlDesc}>
          {displayAutonomy <= 2
            ? "Inconclusive intent checks treated as denials."
            : displayAutonomy >= 9
              ? "Semantic intent check is bypassed entirely."
              : "Semantic checks run normally — inconclusive = allow."}
        </p>
      </div>

      <div className={styles.rateLimit}>
        <span className={styles.controlLabel}>Rate Limit</span>
        <span className={styles.rateLimitValue}>
          {settings ? `${settings.rateLimitMaxRequests} req / ${settings.rateLimitWindowSeconds}s` : "—"}
        </span>
      </div>
    </div>
  );
}
