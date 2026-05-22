import { Settings } from "lucide-react";
import styles from "./settings.module.scss";
import { useSystemSettings } from "../../hooks/useSystemSettings";
import PageHeader from "../../components/ui/page-header";
import SettingsEditor from "./components/settings-editor";

const SettingsPage = () => {
  const { settings, isLoading, save, isSaving, isSaveSuccess, isSaveError, saveError, resetSave } = useSystemSettings();

  if (isLoading || !settings) {
    return (
      <div className={styles.root}>
        <PageHeader
          icon={<Settings size={18} className={styles.titleIcon} />}
          title="Settings"
          subtitle="System configuration and global agent behavior"
        />
        <div className={styles.body}>
          <p className={styles.loadingText}>Loading settings…</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.root}>
      <PageHeader
        icon={<Settings size={18} className={styles.titleIcon} />}
        title="Settings"
        subtitle="System configuration and global agent behaviour"
      />
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
    </div>
  );
};

export default SettingsPage;
