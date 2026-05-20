import { useEffect, useRef, useState } from "react";
import { CheckCircle, Loader, Shield, X, Zap } from "lucide-react";
import styles from "../settings.module.scss";
import StatusMessage from "../../../components/ui/status-message";
import FormField from "../../../components/ui/form-field";

const MAX_CHARS = 16_000;

interface SettingsEditorProps {
  initialPrompt: string;
  initialBuiltIn: boolean;
  updatedBy: string | null;
  save: (payload: { userHarnessPrompt: string; isBuiltInHarnessEnabled: boolean }) => void;
  isSaving: boolean;
  isSaveSuccess: boolean;
  isSaveError: boolean;
  saveError: Error | null;
  resetSave: () => void;
}

const SettingsEditor = ({
  initialPrompt,
  initialBuiltIn,
  updatedBy,
  save,
  isSaving,
  isSaveSuccess,
  isSaveError,
  saveError,
  resetSave,
}: SettingsEditorProps) => {
  const [prompt, setPrompt] = useState(initialPrompt);
  const [builtInEnabled, setBuiltInEnabled] = useState(initialBuiltIn);
  const successTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (isSaveSuccess) {
      successTimeout.current = setTimeout(() => resetSave(), 3000);
    }
    return () => {
      if (successTimeout.current) {
        clearTimeout(successTimeout.current);
      }
    };
  }, [isSaveSuccess, resetSave]);

  const handleSave = () => {
    save({ userHarnessPrompt: prompt, isBuiltInHarnessEnabled: builtInEnabled });
  };

  const charsRemaining = MAX_CHARS - prompt.length;
  const isOverLimit = charsRemaining < 0;
  const isNearLimit = charsRemaining < 500 && !isOverLimit;
  const isDirty = prompt !== initialPrompt || builtInEnabled !== initialBuiltIn;

  return (
    <div className={styles.body}>
      <div className={styles.sectionDivider}>
        <span>Agent Harness</span>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div className={styles.cardHeaderLeft}>
            <Shield size={15} className={styles.cardIconPurple} />
            <div>
              <p className={styles.cardTitle}>Built-in Governance Harness</p>
              <p className={styles.cardSubtitle}>
                Universal agent policy — chain-of-thought, retrieval-first, anti-hallucination
              </p>
            </div>
          </div>
        </div>

        <div className={styles.toggleRow}>
          <div className={styles.toggleInfo}>
            <span className={styles.toggleLabel}>Enable built-in harness</span>
            <span className={styles.toggleDesc}>
              Prepends the Universal Agent Governance policy to every agent interaction. Recommended to keep enabled.
            </span>
          </div>
          <label className={styles.toggleSwitch}>
            <input type="checkbox" checked={builtInEnabled} onChange={(e) => setBuiltInEnabled(e.target.checked)} />
            <span className={styles.slider} />
          </label>
        </div>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div className={styles.cardHeaderLeft}>
            <Zap size={15} className={styles.cardIconAmber} />
            <div>
              <p className={styles.cardTitle}>User-Defined Global Behavior</p>
              <p className={styles.cardSubtitle}>
                Injected into every agent alongside the built-in harness. Define personas, tone, domain focus, or
                operational constraints.
              </p>
            </div>
          </div>
          {updatedBy && <span className={styles.pillPurple}>Last saved by {updatedBy}</span>}
        </div>

        <div className={styles.cardBody}>
          <FormField label="Global Prompt" charCount={{ current: prompt.length, max: MAX_CHARS }}>
            <textarea
              className={`${styles.promptTextarea} ${isOverLimit || isNearLimit ? styles.promptTextareaWarn : ""}`}
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder={`Define global agent behavior in plain text or markdown.\n\nExamples:\n- "Always respond in a professional, concise tone."\n- "You are a local AI orchestrator. Prioritize efficient workflow execution."\n- "When uncertain, ask clarifying questions before acting."`}
              spellCheck={false}
            />
          </FormField>

          {isSaveSuccess && (
            <StatusMessage
              variant="success"
              icon={<CheckCircle size={14} />}
              message="Settings saved. Changes will take effect within 30 seconds."
            />
          )}

          {isSaveError && (
            <StatusMessage
              variant="error"
              icon={<X size={14} />}
              message={saveError?.message ?? "Failed to save settings."}
            />
          )}

          <div className={styles.actionRow}>
            <button className={styles.btnPrimary} onClick={handleSave} disabled={isSaving || isOverLimit || !isDirty}>
              {isSaving ? <Loader size={13} /> : <CheckCircle size={13} />}
              {isSaving ? "Saving…" : "Save Changes"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SettingsEditor;
