import { Link } from "react-router";
import { ArrowUpRight } from "lucide-react";
import { Card } from "@/shared/ui/card";
import { StatusMessage } from "@/shared/ui/status-message";
import type {
  SettingDefinition,
  SettingsEnvelope,
  SettingsGroupValue,
  SettingsSection as Section,
  SettingValue,
} from "../types/settings";
import { SettingRow } from "./setting-row";
import styles from "../settings.module.css";

interface SettingsSectionProps {
  section: Section;
  /** The section's settings left after search and filters. */
  visible: SettingDefinition[];
  envelope: SettingsEnvelope<SettingsGroupValue> | undefined;
  loadError: Error | undefined;
  saveError: Error | undefined;
  inheritZoneLabel: string;
  valueOf: (id: string) => SettingValue;
  isDirty: (id: string) => boolean;
  errors: Record<string, string>;
  onChange: (id: string, value: SettingValue) => void;
}

export function SettingsSection({
  section,
  visible,
  envelope,
  loadError,
  saveError,
  inheritZoneLabel,
  valueOf,
  isDirty,
  errors,
  onChange,
}: SettingsSectionProps) {
  const Icon = section.icon;
  const headingId = `settings-${section.id}-title`;

  return (
    <Card as="section" padding="none" id={section.id} aria-labelledby={headingId} className={styles.section}>
      <header className={styles.sectionHeader}>
        <span className={styles.sectionIcon} aria-hidden="true">
          <Icon size={16} />
        </span>
        <div className={styles.sectionText}>
          <h2 id={headingId} className={styles.sectionTitle}>
            {section.title}
          </h2>
          <p className={styles.sectionDescription}>{section.description}</p>
        </div>
        {envelope?.updatedBy && (
          <span className={styles.sectionMeta} title={new Date(envelope.updatedAt).toLocaleString()}>
            last saved by {envelope.updatedBy}
          </span>
        )}
      </header>

      {section.note && (
        <p className={styles.sectionNote}>
          {section.note.text}{" "}
          <Link to={section.note.to} className={styles.sectionLink}>
            {section.note.linkLabel}
            <ArrowUpRight size={12} aria-hidden="true" />
          </Link>
        </p>
      )}

      {saveError && (
        <StatusMessage
          variant="error"
          className={styles.sectionMessage}
          message={`Not saved: ${saveError.message || "the server rejected the change."}`}
        />
      )}

      {loadError ? (
        <StatusMessage
          variant="error"
          className={styles.sectionMessage}
          message={`Couldn't load these settings: ${loadError.message}`}
        />
      ) : (
        <div className={styles.rows}>
          {visible.map((definition) => (
            <SettingRow
              key={definition.id}
              definition={definition}
              value={valueOf(definition.id)}
              isDirty={isDirty(definition.id)}
              error={errors[definition.id]}
              disabled={!envelope}
              inheritZoneLabel={inheritZoneLabel}
              onChange={(value) => onChange(definition.id, value)}
            />
          ))}
        </div>
      )}
    </Card>
  );
}
