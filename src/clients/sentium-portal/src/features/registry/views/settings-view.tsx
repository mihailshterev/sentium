import { useMemo } from "react";
import styles from "../settings.module.css";
import { useSystemSettings } from "@/features/registry/hooks/useSystemSettings";
import { useInferenceSettings } from "@/features/registry/hooks/useInferenceSettings";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Card } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { SettingsEditor } from "../components/settings-editor";
import type { SettingsEditorFormData } from "../schemas/settings.editor";

export function SettingsView() {
  const { isSovereign } = useRole();

  const {
    settings: harnessSettings,
    isLoading: isHarnessLoading,
    save: saveHarness,
    isSaving: isHarnessSaving,
    isSaveSuccess: isHarnessSuccess,
    isSaveError: isHarnessError,
    saveError: harnessError,
    resetSave: resetHarnessSave,
  } = useSystemSettings();

  const {
    settings: inferenceEnvelope,
    isLoading: isInferenceLoading,
    save: saveInference,
    isSaving: isInferenceSaving,
    isSaveSuccess: isInferenceSuccess,
    isSaveError: isInferenceError,
    saveError: inferenceError,
    resetSave: resetInferenceSave,
  } = useInferenceSettings(isSovereign);

  const isGlobalLoading = isHarnessLoading || (isSovereign && isInferenceLoading);

  const initialUnifiedValues: SettingsEditorFormData = useMemo(
    () => ({
      userHarnessPrompt: harnessSettings?.harness.userHarnessPrompt ?? "",
      isBuiltInHarnessEnabled: harnessSettings?.harness.isBuiltInHarnessEnabled ?? true,
      isPromptEnhancementEnabled: harnessSettings?.harness.isPromptEnhancementEnabled ?? false,
      defaultModel: inferenceEnvelope?.value?.defaultModel ?? "",
      agentTemperature: inferenceEnvelope?.value?.agentTemperature ?? 0.3,
    }),
    [harnessSettings, inferenceEnvelope],
  );

  if (isGlobalLoading || !harnessSettings) {
    return (
      <Page title="Settings" subtitle="System configuration and global agent behavior">
        <Card padding="lg" aria-busy="true">
          <p className={styles.loadingText}>Loading settings…</p>
          <SkeletonText lines={4} />
        </Card>
      </Page>
    );
  }

  const handleGlobalSubmit = (data: SettingsEditorFormData) => {
    saveHarness({
      harness: {
        userHarnessPrompt: data.userHarnessPrompt,
        isBuiltInHarnessEnabled: data.isBuiltInHarnessEnabled,
        isPromptEnhancementEnabled: data.isPromptEnhancementEnabled,
      },
    });

    if (isSovereign) {
      saveInference({
        defaultModel: data.defaultModel,
        agentTemperature: data.agentTemperature,
      });
    }
  };

  const handleGlobalReset = () => {
    resetHarnessSave();
    resetInferenceSave();
  };

  const combinedSaving = isHarnessSaving || isInferenceSaving;
  const combinedSuccess = isHarnessSuccess || isInferenceSuccess;
  const combinedError = isHarnessError || isInferenceError;
  const activeErrorMessage = harnessError?.message || (inferenceError as Error)?.message || null;

  return (
    <Page title="Settings" subtitle="System configuration and global agent behavior">
      <SettingsEditor
        initialValues={initialUnifiedValues}
        isSovereign={isSovereign}
        updatedByHarness={harnessSettings.updatedBy ?? null}
        updatedByInference={inferenceEnvelope?.updatedBy ?? null}
        onSubmitForm={handleGlobalSubmit}
        isSaving={combinedSaving}
        showGlobalSuccess={combinedSuccess}
        showGlobalError={combinedError}
        globalErrorMessage={activeErrorMessage}
        resetSaveStates={handleGlobalReset}
      />
    </Page>
  );
}
