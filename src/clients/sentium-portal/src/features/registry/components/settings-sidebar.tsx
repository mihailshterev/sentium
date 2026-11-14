import { SearchInput } from "@/shared/ui/form-controls";
import type { SettingScope, SettingsSection } from "../types/settings";
import styles from "../settings.module.css";

export interface SidebarGroup {
  scope: SettingScope;
  title: string;
  description: string;
  sections: { section: SettingsSection; visibleCount: number; hasUnsaved: boolean }[];
}

interface SettingsSidebarProps {
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
