import { useEffect, useRef, useState } from "react";
import { CheckCircle, Loader, Settings, Shield, X, Zap } from "lucide-react";
import styles from "./settings.module.scss";
import { useSystemSettings } from "../../hooks/useSystemSettings";

const MAX_CHARS = 16_000;

const SettingsPage = () => {
  const { settings, isLoading, save, isSaving, isSaveSuccess, isSaveError, saveError, resetSave } = useSystemSettings();

  if (isLoading || !settings) {
    return (
      <div className={styles.root}>
        <div className={styles.header}>
          <div className={styles.headerLeft}>
            <Settings size={18} className={styles.titleIcon} />
            <div>
              <h1 className={styles.pageTitle}>Settings</h1>
              <p className={styles.pageSubtitle}>System configuration and global agent behaviour</p>
            </div>
          </div>
        </div>
        <div className={styles.body}>
          <p className={styles.loadingText}>Loading settings…</p>
        </div>
      </div>
    );
  }

  return (
    <SettingsEditor
      key={`${settings.updatedAt}`}
      initialPrompt={settings.userHarnessPrompt}
      initialBuiltIn={settings.isBuiltInHarnessEnabled}
      updatedBy={settings.updatedBy ?? null}
      save={save}
      isSaving={isSaving}
      isSaveSuccess={isSaveSuccess}
      isSaveError={isSaveError}
      saveError={saveError}
      resetSave={resetSave}
    />
  );
};

interface EditorProps {
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
}: EditorProps) => {
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
    <div className={styles.root}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Settings size={18} className={styles.titleIcon} />
          <div>
            <h1 className={styles.pageTitle}>Settings</h1>
            <p className={styles.pageSubtitle}>System configuration and global agent behaviour</p>
          </div>
        </div>
      </div>

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
                <p className={styles.cardTitle}>User-Defined Global Behaviour</p>
                <p className={styles.cardSubtitle}>
                  Injected into every agent alongside the built-in harness. Define personas, tone, domain focus, or
                  operational constraints.
                </p>
              </div>
            </div>
            {updatedBy && <span className={styles.pillPurple}>Last saved by {updatedBy}</span>}
          </div>

          <div className={styles.cardBody}>
            <textarea
              className={styles.promptTextarea}
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder={`Define global agent behaviour in plain text or markdown.\n\nExamples:\n- "Always respond in a professional, concise tone."\n- "You are a security-focused AI. Prioritise threat identification."\n- "When uncertain, ask clarifying questions before acting."`}
              spellCheck={false}
            />
            <p className={`${styles.charCount} ${isOverLimit || isNearLimit ? styles.charCountWarn : ""}`}>
              {isOverLimit
                ? `${Math.abs(charsRemaining)} characters over limit`
                : `${charsRemaining.toLocaleString()} characters remaining`}
            </p>

            {isSaveSuccess && (
              <div className={`${styles.alert} ${styles.alertSuccess}`}>
                <CheckCircle size={14} />
                Settings saved. Changes will take effect within 30 seconds.
              </div>
            )}

            {isSaveError && (
              <div className={`${styles.alert} ${styles.alertError}`}>
                <X size={14} />
                {saveError?.message ?? "Failed to save settings."}
              </div>
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
    </div>
  );
};

export default SettingsPage;
