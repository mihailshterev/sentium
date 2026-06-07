import { BrainCircuit, Shield, ShieldAlert, Zap } from "lucide-react";
import styles from "../sentinel.module.scss";
import ModelSelector from "../../../components/ui/model-selector";
import type { PdpSettings } from "../../../types/sentinel";
import type { OllamaModel } from "../../../types/models";

interface SovereignControlsProps {
  settings: PdpSettings | undefined;
  isUpdating: boolean;
  displayAutonomy: number;
  models: OllamaModel[];
  onLockdown: () => void;
  onSemanticToggle: () => void;
  onAutonomyChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
  commitAutonomy: () => void;
  onIntentModelChange: (model: string) => void;
}

const SovereignControls = ({
  settings,
  isUpdating,
  displayAutonomy,
  models,
  onLockdown,
  onSemanticToggle,
  onAutonomyChange,
  commitAutonomy,
  onIntentModelChange,
}: SovereignControlsProps) => (
  <div className={styles.cardBody}>
    <div className={styles.toggleRow}>
      <div className={styles.toggleInfo}>
        <span className={styles.toggleLabel}>
          <ShieldAlert size={13} />
          Lockdown Mode
        </span>
        <p className={styles.toggleDesc}>Deny all non-Read agent actions immediately.</p>
      </div>
      <button
        className={`${styles.toggle} ${settings?.lockdownMode ? styles.toggleOn : ""}`}
        onClick={onLockdown}
        disabled={isUpdating || !settings}
        aria-pressed={settings?.lockdownMode}
        data-testid="lockdown-toggle"
      >
        <span className={styles.toggleThumb} />
      </button>
    </div>

    <div className={styles.toggleRow}>
      <div className={styles.toggleInfo}>
        <span className={styles.toggleLabel}>
          <Shield size={13} />
          Semantic Intent Check
        </span>
        <p className={styles.toggleDesc}>LLM verification of agent intent alignment.</p>
      </div>
      <button
        className={`${styles.toggle} ${settings?.semanticIntentCheckEnabled ? styles.toggleOn : ""}`}
        onClick={onSemanticToggle}
        disabled={isUpdating || !settings}
        aria-pressed={settings?.semanticIntentCheckEnabled}
        data-testid="semantic-intent-toggle"
      >
        <span className={styles.toggleThumb} />
      </button>
    </div>

    <div className={styles.modelSelectRow}>
      <div className={styles.toggleInfo}>
        <span className={styles.toggleLabel}>
          <BrainCircuit size={13} />
          Intent Check Model
        </span>
        <p className={styles.toggleDesc}>Model used for semantic intent verification.</p>
      </div>
      <ModelSelector
        models={models.map((m) => m.name)}
        value={settings?.intentCheckModel ?? ""}
        onChange={onIntentModelChange}
        disabled={isUpdating || !settings}
        className={styles.modelSelectorField}
      />
    </div>

    <div className={styles.sliderSection}>
      <div className={styles.sliderHeader}>
        <span className={styles.toggleLabel}>
          <Zap size={13} />
          AI Autonomy
        </span>
        <span className={styles.sliderValue}>
          {displayAutonomy <= 2 ? "Max Security" : displayAutonomy >= 9 ? "Max Autonomy" : `Level ${displayAutonomy}`}
        </span>
      </div>
      <div className={styles.sliderLabels}>
        <span>High Security</span>
        <span>High Autonomy</span>
      </div>
      <input
        type="range"
        min={1}
        max={10}
        step={1}
        value={displayAutonomy}
        onChange={onAutonomyChange}
        onMouseUp={commitAutonomy}
        onTouchEnd={commitAutonomy}
        onKeyUp={commitAutonomy}
        disabled={isUpdating || !settings}
        className={styles.slider}
        data-testid="autonomy-slider"
      />
      <p className={styles.toggleDesc}>
        {displayAutonomy <= 2
          ? "Inconclusive intent checks treated as denials."
          : displayAutonomy >= 9
            ? "Semantic intent check is bypassed entirely."
            : "Semantic checks run normally — inconclusive = allow."}
      </p>
    </div>

    <div className={styles.rateLimitRow}>
      <span className={styles.toggleLabel}>Rate Limit</span>
      <span className={styles.rateLimitValue}>
        {settings ? `${settings.rateLimitMaxRequests} req / ${settings.rateLimitWindowSeconds}s` : "—"}
      </span>
    </div>
  </div>
);

export default SovereignControls;
