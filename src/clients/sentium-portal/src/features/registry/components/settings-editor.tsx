import { useEffect, useRef } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle, Cpu, Shield, Sparkles, X, Zap } from "lucide-react";
import styles from "../settings.module.css";
import { useOllamaModels } from "@/features/models/hooks/useOllamaModels";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Card, CardHeader } from "@/shared/ui/card";
import { Range, SwitchRow, Textarea } from "@/shared/ui/form-controls";
import { FormField } from "@/shared/ui/form-field";
import { ModelSelector } from "@/shared/ui/model-selector";
import { StatusMessage } from "@/shared/ui/status-message";
import { settingsEditorSchema, type SettingsEditorFormData } from "../schemas/settings.editor";

interface SettingsEditorProps {
  initialValues: SettingsEditorFormData;
  isSovereign: boolean;
  updatedByHarness: string | null;
  updatedByInference: string | null;
  onSubmitForm: (data: SettingsEditorFormData) => void;
  isSaving: boolean;
  showGlobalSuccess: boolean;
  showGlobalError: boolean;
  globalErrorMessage: string | null;
  resetSaveStates: () => void;
}

const PROMPT_LIMIT = 16000;

export function SettingsEditor({
  initialValues,
  isSovereign,
  updatedByHarness,
  updatedByInference,
  onSubmitForm,
  isSaving,
  showGlobalSuccess,
  showGlobalError,
  globalErrorMessage,
  resetSaveStates,
}: SettingsEditorProps) {
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
      if (successTimeout.current) {
        clearTimeout(successTimeout.current);
      }
    };
  }, [showGlobalSuccess, resetSaveStates]);

  const charsRemaining = PROMPT_LIMIT - prompt.length;
  const isOverLimit = charsRemaining < 0;
  const isNearLimit = charsRemaining < 500 && !isOverLimit;

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className={styles.form}>
      <h2 className={styles.group}>Agent Harness</h2>

      <Card padding="md" as="section">
        <CardHeader
          icon={<Shield />}
          title="Built-in Governance Harness"
          description="Universal agent policy — chain-of-thought, retrieval-first, anti-hallucination"
        />
        <Controller
          name="isBuiltInHarnessEnabled"
          control={control}
          render={({ field }) => (
            <SwitchRow
              label="Enable built-in harness"
              description="Prepends the Universal Agent Governance policy to every agent interaction. Recommended to keep enabled."
              checked={field.value}
              onChange={(e) => field.onChange(e.target.checked)}
            />
          )}
        />
      </Card>

      <Card padding="md" as="section">
        <CardHeader
          icon={<Sparkles />}
          title="Prompt Enhancement"
          description="Optimizes each prompt with a pre-pass before execution — sharper results from smaller local models"
        />
        <Controller
          name="isPromptEnhancementEnabled"
          control={control}
          render={({ field }) => (
            <SwitchRow
              label="Enable prompt enhancement"
              description="Rewrites assistant and workflow prompts for clarity and specificity before the agent runs. The original is preserved and the enhanced version is shown inline."
              checked={field.value}
              onChange={(e) => field.onChange(e.target.checked)}
            />
          )}
        />
      </Card>

      <Card padding="md" as="section">
        <CardHeader
          icon={<Zap />}
          title="User-Defined Global Behavior"
          description="Injected into every agent alongside the built-in harness. Define personas, tone, domain focus, or operational constraints."
          actions={updatedByHarness && <Badge tone="neutral">Last saved by {updatedByHarness}</Badge>}
        />
        <FormField
          id="global-prompt"
          label="Global Prompt"
          charCount={{ current: prompt.length, max: PROMPT_LIMIT }}
          error={isOverLimit ? `${Math.abs(charsRemaining)} characters over limit` : undefined}
          hint={isNearLimit ? `${charsRemaining} characters remaining` : undefined}
        >
          <Textarea
            id="global-prompt"
            mono
            rows={10}
            invalid={isOverLimit}
            placeholder={`Define global agent behavior in plain text or markdown.\n\nExamples:\n- "Always respond in a professional, concise tone."\n- "You are a local AI orchestrator. Prioritize efficient workflow execution."`}
            spellCheck={false}
            {...register("userHarnessPrompt")}
          />
        </FormField>
      </Card>

      {isSovereign && (
        <>
          <h2 className={styles.group}>Inference</h2>

          <Card padding="md" as="section">
            <CardHeader
              icon={<Cpu />}
              title="Inference Settings"
              description="Model and sampling parameters applied to every agent. Changes take effect on the next agent call. Server endpoints and per-model context size are deployment configuration."
              actions={updatedByInference && <Badge tone="neutral">Last saved by {updatedByInference}</Badge>}
            />

            <div className={styles.fields}>
              <div className={styles.fieldRow}>
                <span className={styles.fieldLabel}>Default Model</span>
                <Controller
                  name="defaultModel"
                  control={control}
                  render={({ field }) => (
                    <ModelSelector
                      models={models.map((m) => m.name)}
                      value={field.value}
                      onChange={field.onChange}
                      variant="chip"
                    />
                  )}
                />
              </div>

              <div className={styles.field}>
                <label className={styles.fieldLabel} htmlFor="agent-temperature">
                  Agent Temperature
                  <span className={styles.fieldValue}>{currentTemperature.toFixed(2)}</span>
                </label>
                <Range
                  id="agent-temperature"
                  min={0}
                  max={1}
                  step={0.05}
                  fillPercent={currentTemperature * 100}
                  {...register("agentTemperature", { valueAsNumber: true })}
                />
                <div className={styles.rangeLabels}>
                  <span>Precise (0.0)</span>
                  <span>Creative (1.0)</span>
                </div>
              </div>
            </div>
          </Card>
        </>
      )}

      <div className={styles.saveBar} data-dirty={isDirty || undefined}>
        <div className={styles.saveStatus}>
          {showGlobalSuccess ? (
            <StatusMessage variant="success" icon={<CheckCircle />} message="System parameters successfully saved." />
          ) : showGlobalError ? (
            <StatusMessage
              variant="error"
              icon={<X />}
              message={globalErrorMessage ?? "Failed to save configuration updates."}
            />
          ) : (
            <span className={styles.saveHint}>{isDirty ? "You have unsaved changes" : "All changes saved"}</span>
          )}
        </div>
        <Button
          type="submit"
          variant="primary"
          icon={<CheckCircle size={15} />}
          loading={isSaving}
          disabled={isOverLimit || !isDirty}
        >
          {isSaving ? "Saving System Settings..." : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
