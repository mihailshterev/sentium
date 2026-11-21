import type { LucideIcon } from "lucide-react";
import { SearchInput } from "@/shared/ui/form-controls";
import type { SettingScope, SettingsSection } from "../types/settings";
import styles from "../settings.module.css";

export const LOCAL_GROUP_TITLE = "This browser";
export const LOCAL_GROUP_DESCRIPTION = "Saved on this device only";

export interface SidebarGroup {
  scope: SettingScope;
  title: string;
  description: string;
  sections: { section: SettingsSection; visibleCount: number; hasUnsaved: boolean }[];
}

/** A client-side section outside the settings schema, such as Appearance. */
export interface SidebarLocalItem {
  id: string;
  title: string;
  icon: LucideIcon;
}

interface SettingsSidebarProps {
  /** Shown first, under a "This browser" heading; empty hides the group. */
  localItems: SidebarLocalItem[];
  groups: SidebarGroup[];
  activeId: string | null;
  query: string;
  modifiedOnly: boolean;
  isFiltering: boolean;
  onQueryChange: (query: string) => void;
  onModifiedOnlyChange: (modifiedOnly: boolean) => void;
  onSelect: (id: string) => void;
}

export function SettingsSidebar({
  localItems,
  groups,
  activeId,
  query,
  modifiedOnly,
  isFiltering,
  onQueryChange,
  onModifiedOnlyChange,
  onSelect,
}: SettingsSidebarProps) {
  return (
    <aside className={styles.sidebar}>
      <SearchInput
        controlSize="sm"
        placeholder="Search settings"
        aria-label="Search settings"
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        onKeyDown={(e) => e.key === "Escape" && onQueryChange("")}
      />

      <button
        type="button"
        className={styles.filterChip}
        aria-pressed={modifiedOnly}
        onClick={() => onModifiedOnlyChange(!modifiedOnly)}
      >
        Modified only
      </button>

      <nav aria-label="Settings sections" className={styles.nav}>
        {localItems.length > 0 && (
          <div className={styles.navGroup}>
            <p className={styles.navGroupTitle} title={LOCAL_GROUP_DESCRIPTION}>
              {LOCAL_GROUP_TITLE}
            </p>
            <ul className={styles.navList}>
              {localItems.map(({ id, title, icon: Icon }) => (
                <li key={id}>
                  <button
                    type="button"
                    className={styles.navItem}
                    aria-current={activeId === id ? "true" : undefined}
                    onClick={() => onSelect(id)}
                  >
                    <Icon size={15} aria-hidden="true" />
                    <span className={styles.navLabel}>{title}</span>
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )}
        {groups.map((group) => (
          <div key={group.scope} className={styles.navGroup}>
            <p className={styles.navGroupTitle} title={group.description}>
              {group.title}
            </p>
            <ul className={styles.navList}>
              {group.sections.map(({ section, visibleCount, hasUnsaved }) => {
                const Icon = section.icon;
                const isEmpty = isFiltering && visibleCount === 0;
                return (
                  <li key={section.id}>
                    <button
                      type="button"
                      className={styles.navItem}
                      aria-current={activeId === section.id ? "true" : undefined}
                      disabled={isEmpty}
                      onClick={() => onSelect(section.id)}
                    >
                      <Icon size={15} aria-hidden="true" />
                      <span className={styles.navLabel}>{section.title}</span>
                      {hasUnsaved && <span className={styles.navDirty} aria-label="unsaved changes" />}
                      {isFiltering && <span className={styles.navCount}>{visibleCount}</span>}
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>
    </aside>
  );
}
