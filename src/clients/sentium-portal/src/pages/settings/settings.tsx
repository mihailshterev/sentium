import { useMemo } from "react";
import { Loader, Settings } from "lucide-react";
import styles from "./settings.module.scss";
import { useSystemSettings } from "../../hooks/useSystemSettings";
import { useOllamaSettings } from "../../hooks/useOllamaSettings";
import { useRole } from "../../hooks/useRole";
import PageHeader from "../../components/ui/page-header";
import SettingsEditor from "./components/settings-editor";
import type { SettingsEditorFormData } from "../../schemas/settings.editor";

const SettingsPage = () => {
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
    settings: ollamaEnvelope,
    isLoading: isOllamaLoading,
    save: saveOllama,
    isSaving: isOllamaSaving,
    isSaveSuccess: isOllamaSuccess,
    isSaveError: isOllamaError,
    saveError: ollamaError,
    resetSave: resetOllamaSave,
  } = useOllamaSettings(isSovereign);

  const isGlobalLoading = isHarnessLoading || (isSovereign && isOllamaLoading);

  const initialUnifiedValues: SettingsEditorFormData = useMemo(
    () => ({
      userHarnessPrompt: harnessSettings?.harness.userHarnessPrompt ?? "",
      isBuiltInHarnessEnabled: harnessSettings?.harness.isBuiltInHarnessEnabled ?? true,
      isPromptEnhancementEnabled: harnessSettings?.harness.isPromptEnhancementEnabled ?? false,
      defaultModel: ollamaEnvelope?.value?.defaultModel ?? "",
      agentTemperature: ollamaEnvelope?.value?.agentTemperature ?? 0.3,
      agentContextWindow: ollamaEnvelope?.value?.agentContextWindow ?? 16384,
    }),
    [harnessSettings, ollamaEnvelope],
  );

  if (isGlobalLoading || !harnessSettings) {
    return (
      <div className={styles.root}>
        <PageHeader
          icon={<Settings size={18} className={styles.titleIcon} />}
          title="Settings"
          subtitle="System configuration and global agent behavior"
        />
        <div className={styles.body}>
          <p className={styles.loadingText}>
            <Loader size={15} className="animate-spin" />
            Loading settings system variables…
          </p>
        </div>
      </div>
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
      saveOllama({
        defaultModel: data.defaultModel,
        agentTemperature: data.agentTemperature,
        agentContextWindow: data.agentContextWindow,
      });
    }
  };

  const handleGlobalReset = () => {
    resetHarnessSave();
    resetOllamaSave();
  };

  const combinedSaving = isHarnessSaving || isOllamaSaving;
  const combinedSuccess = isHarnessSuccess || isOllamaSuccess;
  const combinedError = isHarnessError || isOllamaError;
  const activeErrorMessage = harnessError?.message || (ollamaError as Error)?.message || null;

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<Settings size={18} className={styles.titleIcon} />}
        title="Settings"
        subtitle="System configuration and global agent behavior"
      />
      <SettingsEditor
        initialValues={initialUnifiedValues}
        isSovereign={isSovereign}
        updatedByHarness={harnessSettings.updatedBy ?? null}
        updatedByOllama={ollamaEnvelope?.updatedBy ?? null}
        onSubmitForm={handleGlobalSubmit}
        isSaving={combinedSaving}
        showGlobalSuccess={combinedSuccess}
        showGlobalError={combinedError}
        globalErrorMessage={activeErrorMessage}
        resetSaveStates={handleGlobalReset}
      />
    </div>
  );
};

export default SettingsPage;
