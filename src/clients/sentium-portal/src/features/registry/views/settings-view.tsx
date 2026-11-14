import { useMemo } from "react";
import styles from "../settings.module.css";
import { useSystemSettings } from "@/features/registry/hooks/useSystemSettings";
import { useInferenceSettings } from "@/features/registry/hooks/useInferenceSettings";
import { useCalendarSettings } from "@/features/registry/hooks/useCalendarSettings";
import { useRole } from "@/shared/auth/hooks/useRole";
import { Card } from "@/shared/ui/card";
import { Page } from "@/shared/ui/page";
import { SkeletonText } from "@/shared/ui/skeleton";
import { SettingsEditor } from "../components/settings-editor";
import type { SettingsEditorFormData } from "../schemas/settings.editor";

/** `HH:mm:ss` from the server to the `HH:mm` of a time input, and back (empty means "not set"). */
const toTimeInput = (time: string | null | undefined) => (time ? time.slice(0, 5) : "");
const fromTimeInput = (time: string) => (time ? `${time}:00` : null);

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

  const calendar = useCalendarSettings(isSovereign);

  const isGlobalLoading = isHarnessLoading || (isSovereign && isInferenceLoading) || calendar.isLoading;

  const initialUnifiedValues: SettingsEditorFormData = useMemo(
    () => ({
      userHarnessPrompt: harnessSettings?.harness.userHarnessPrompt ?? "",
      isBuiltInHarnessEnabled: harnessSettings?.harness.isBuiltInHarnessEnabled ?? true,
      isPromptEnhancementEnabled: harnessSettings?.harness.isPromptEnhancementEnabled ?? false,
      defaultModel: inferenceEnvelope?.value?.defaultModel ?? "",
      agentTemperature: inferenceEnvelope?.value?.agentTemperature ?? 0.3,
      timeZone: calendar.own?.timeZone ?? "",
      dayStart: toTimeInput(calendar.own?.dayStart),
      householdTimeZone: calendar.household?.timeZone ?? "UTC",
      householdDayStart: toTimeInput(calendar.household?.dayStart) || "08:00",
    }),
    [harnessSettings, inferenceEnvelope, calendar.own, calendar.household],
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

    const ownChanged =
      data.timeZone !== initialUnifiedValues.timeZone || data.dayStart !== initialUnifiedValues.dayStart;
    const householdChanged =
      isSovereign &&
      (data.householdTimeZone !== initialUnifiedValues.householdTimeZone ||
        data.householdDayStart !== initialUnifiedValues.householdDayStart);

    if (ownChanged || householdChanged) {
      calendar.save({
        own: { timeZone: data.timeZone || null, dayStart: fromTimeInput(data.dayStart) },
        ...(householdChanged && {
          household: { timeZone: data.householdTimeZone, dayStart: `${data.householdDayStart}:00` },
        }),
      });
    }
  };

  const handleGlobalReset = () => {
    resetHarnessSave();
    resetInferenceSave();
    calendar.resetSave();
  };

  const combinedSaving = isHarnessSaving || isInferenceSaving || calendar.isSaving;
  const combinedSuccess = isHarnessSuccess || isInferenceSuccess || calendar.isSaveSuccess;
  const combinedError = isHarnessError || isInferenceError || calendar.isSaveError;
  const activeErrorMessage =
    harnessError?.message || inferenceError?.message || calendar.saveError?.message || null;

  return (
    <Page title="Settings" subtitle="System configuration and global agent behavior">
      <SettingsEditor
        initialValues={initialUnifiedValues}
        isSovereign={isSovereign}
        updatedByHarness={harnessSettings.updatedBy ?? null}
        updatedByInference={inferenceEnvelope?.updatedBy ?? null}
        householdTimeZone={calendar.household?.timeZone ?? null}
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
