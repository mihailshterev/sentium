import { useEffect, useRef } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle, Cpu, Loader, Shield, Sparkles, X, Zap } from "lucide-react";
import styles from "../settings.module.scss";
import StatusMessage from "../../../components/ui/status-message";
import FormField from "../../../components/ui/form-field";
import ModelSelector from "../../../components/ui/model-selector";
import { settingsEditorSchema, type SettingsEditorFormData } from "../../../schemas/settings.editor";
import useOllamaModels from "../../../hooks/useOllamaModels";

interface SettingsEditorProps {
  initialValues: SettingsEditorFormData;
  isSovereign: boolean;
  updatedByHarness: string | null;
  updatedByOllama: string | null;
  onSubmitForm: (data: SettingsEditorFormData) => void;
  isSaving: boolean;
  showGlobalSuccess: boolean;
  showGlobalError: boolean;
  globalErrorMessage: string | null;
  resetSaveStates: () => void;
}

const SettingsEditor = ({
  initialValues,
  isSovereign,
  updatedByHarness,
  updatedByOllama,
  onSubmitForm,
  isSaving,
  showGlobalSuccess,
  showGlobalError,
  globalErrorMessage,
  resetSaveStates,
}: SettingsEditorProps) => {
  const { models } = useOllamaModels();

  const {
    register,
    handleSubmit,
    control,
    watch,
    reset,
    formState: { isDirty },
  } = useForm<SettingsEditorFormData>({
    resolver: zodResolver(settingsEditorSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    reset(initialValues);
  }, [initialValues, reset]);

  // eslint-disable-next-line react-hooks/incompatible-library
  const prompt = watch("userHarnessPrompt") ?? "";
  const currentTemperature = watch("agentTemperature") ?? 0.3;

  const successTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => {
    if (showGlobalSuccess) {
      successTimeout.current = setTimeout(() => resetSaveStates(), 4000);
    }
    return () => {
      if (successTimeout.current) clearTimeout(successTimeout.current);
    };
  }, [showGlobalSuccess, resetSaveStates]);

  const charsRemaining = 16000 - prompt.length;
  const isOverLimit = charsRemaining < 0;
  const isNearLimit = charsRemaining < 500 && !isOverLimit;

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className={styles.body}>
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
            <Controller
              name="isBuiltInHarnessEnabled"
              control={control}
              render={({ field }) => (
                <input type="checkbox" checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />
              )}
            />
            <span className={styles.slider} />
          </label>
        </div>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div className={styles.cardHeaderLeft}>
            <Sparkles size={15} className={styles.cardIconCyan} />
            <div>
              <p className={styles.cardTitle}>Prompt Enhancement</p>
              <p className={styles.cardSubtitle}>
                Optimizes each prompt with a pre-pass before execution — sharper results from smaller local models
              </p>
            </div>
          </div>
        </div>

        <div className={styles.toggleRow}>
          <div className={styles.toggleInfo}>
            <span className={styles.toggleLabel}>Enable prompt enhancement</span>
            <span className={styles.toggleDesc}>
              Rewrites assistant and workflow prompts for clarity and specificity before the agent runs. The original is
              preserved and the enhanced version is shown inline.
            </span>
          </div>
          <label className={styles.toggleSwitch}>
            <Controller
              name="isPromptEnhancementEnabled"
              control={control}
              render={({ field }) => (
                <input type="checkbox" checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />
              )}
            />
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
          {updatedByHarness && <span className={styles.pillPurple}>Last saved by {updatedByHarness}</span>}
        </div>

        <div className={styles.cardBody}>
          <FormField label="Global Prompt" charCount={{ current: prompt.length, max: 16000 }}>
            {isOverLimit && <span>{Math.abs(charsRemaining)} characters over limit</span>}
            {isNearLimit && <span>{charsRemaining} characters remaining</span>}
            <textarea
              className={`${styles.promptTextarea} ${isOverLimit || isNearLimit ? styles.promptTextareaWarn : ""}`}
              placeholder={`Define global agent behavior in plain text or markdown.\n\nExamples:\n- "Always respond in a professional, concise tone."\n- "You are a local AI orchestrator. Prioritize efficient workflow execution."`}
              spellCheck={false}
              {...register("userHarnessPrompt")}
            />
          </FormField>
        </div>
      </div>

      {isSovereign && (
        <>
          <div className={styles.sectionDivider}>
            <span>Ollama</span>
          </div>

          <div className={styles.card}>
            <div className={styles.cardHeader}>
              <div className={styles.cardHeaderLeft}>
                <Cpu size={15} className={styles.cardIconCyan} />
                <div>
                  <p className={styles.cardTitle}>Ollama Inference Settings</p>
                  <p className={styles.cardSubtitle}>
                    Model and inference parameters applied to every agent. Changes take effect on the next agent call.
                    BaseUrl is configured via environment variables.
                  </p>
                </div>
              </div>
              {updatedByOllama && <span className={styles.pillPurple}>Last saved by {updatedByOllama}</span>}
            </div>

            <div className={styles.cardBody}>
              <div className={styles.fieldRow}>
                <label className={styles.fieldLabel}>Default Model</label>
                <Controller
                  name="defaultModel"
                  control={control}
                  render={({ field }) => (
                    <ModelSelector
                      models={models.map((m) => m.name)}
                      value={field.value}
                      onChange={field.onChange}
                      className={styles.modelSelectorField}
                    />
                  )}
                />
              </div>

              <div className={styles.fieldRow}>
                <label className={styles.fieldLabel}>
                  Agent Temperature
                  <span className={styles.fieldValue}>{currentTemperature.toFixed(2)}</span>
                </label>
                <input
                  type="range"
                  min={0}
                  max={1}
                  step={0.05}
                  className={styles.rangeField}
                  {...register("agentTemperature", { valueAsNumber: true })}
                />
                <div className={styles.rangeLabels}>
                  <span>Precise (0.0)</span>
                  <span>Creative (1.0)</span>
                </div>
              </div>

              <div className={styles.fieldRow}>
                <label className={styles.fieldLabel}>Context Window (tokens)</label>
                <input
                  type="number"
                  min={512}
                  max={131072}
                  step={512}
                  className={styles.numberField}
                  {...register("agentContextWindow", { valueAsNumber: true })}
                />
              </div>
            </div>
          </div>
        </>
      )}

      <div className={styles.formFooter}>
        {showGlobalSuccess && (
          <StatusMessage
            variant="success"
            icon={<CheckCircle size={14} />}
            message="System parameters successfully saved."
          />
        )}
        {showGlobalError && (
          <StatusMessage
            variant="error"
            icon={<X size={14} />}
            message={globalErrorMessage ?? "Failed to save configuration updates."}
          />
        )}

        <div className={styles.actionRow}>
          <button type="submit" className={styles.btnPrimary} disabled={isSaving || isOverLimit || !isDirty}>
            {isSaving ? <Loader size={13} className="animate-spin" /> : <CheckCircle size={13} />}
            {isSaving ? "Saving System Settings..." : "Save changes"}
          </button>
        </div>
      </div>
    </form>
  );
};

export default SettingsEditor;
