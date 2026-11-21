import type { CSSProperties } from "react";
import { Monitor, Moon, Sun, type LucideIcon } from "lucide-react";
import { Card } from "@/shared/ui/card";
import { PALETTES, type Palette } from "@/shared/ui/state/palettes";
import { resolveTheme, useThemeStore, type ThemePreference } from "@/shared/ui/state/theme-store";
import { APPEARANCE_ICON as PaletteIcon, APPEARANCE_SECTION_ID, APPEARANCE_TITLE } from "../catalog/appearance";
import styles from "../settings.module.css";
import appearance from "../appearance.module.css";

const MODES: { id: ThemePreference; label: string; icon: LucideIcon }[] = [
  { id: "system", label: "System", icon: Monitor },
  { id: "dark", label: "Dark", icon: Moon },
  { id: "light", label: "Light", icon: Sun },
];

/**
 * Mode and palette. Unlike the other sections these live in this browser only and apply immediately, so they
 * never touch the draft or the save bar.
 */
export function AppearanceSection() {
  const preference = useThemeStore((s) => s.preference);
  const palette = useThemeStore((s) => s.palette);
  const setPreference = useThemeStore((s) => s.setPreference);
  const setPalette = useThemeStore((s) => s.setPalette);
  const mode = resolveTheme(preference);
  const headingId = `settings-${APPEARANCE_SECTION_ID}-title`;
  const modeLabelId = `settings-${APPEARANCE_SECTION_ID}-mode`;
  const paletteLabelId = `settings-${APPEARANCE_SECTION_ID}-palette`;

  return (
    <Card
      as="section"
      padding="none"
      id={APPEARANCE_SECTION_ID}
      aria-labelledby={headingId}
      className={styles.section}
    >
      <header className={styles.sectionHeader}>
        <span className={styles.sectionIcon} aria-hidden="true">
          <PaletteIcon size={16} />
        </span>
        <div className={styles.sectionText}>
          <h2 id={headingId} className={styles.sectionTitle}>
            {APPEARANCE_TITLE}
          </h2>
          <p className={styles.sectionDescription}>
            Colour palette and light or dark mode. Saved in this browser and applied straight away.
          </p>
        </div>
      </header>

      <div className={styles.rows}>
        <div className={styles.row}>
          <span id={modeLabelId} className={styles.rowTitle}>
            Mode
          </span>
          <div role="radiogroup" aria-labelledby={modeLabelId} className={appearance.modes}>
            {MODES.map(({ id, label, icon: Icon }) => (
              <label key={id} className={appearance.mode}>
                <input
                  type="radio"
                  name="appearance-mode"
                  value={id}
                  checked={preference === id}
                  onChange={() => setPreference(id)}
                  className={appearance.radio}
                />
                <Icon size={14} aria-hidden="true" />
                {label}
              </label>
            ))}
          </div>
        </div>

        <div className={styles.row} data-layout="stacked">
          <span id={paletteLabelId} className={styles.rowTitle}>
            Palette
          </span>
          <div role="radiogroup" aria-labelledby={paletteLabelId} className={appearance.palettes}>
            {PALETTES.map((p) => (
              <PaletteOption
                key={p.id}
                palette={p}
                mode={mode}
                checked={palette === p.id}
                onSelect={() => setPalette(p.id)}
              />
            ))}
          </div>
        </div>
      </div>
    </Card>
  );
}

interface PaletteOptionProps {
  palette: Palette;
  mode: "dark" | "light";
  checked: boolean;
  onSelect: () => void;
}

function PaletteOption({ palette, mode, checked, onSelect }: PaletteOptionProps) {
  const swatch = palette.swatches[mode];
  const previewStyle = {
    "--preview-base": swatch.base,
    "--preview-surface": swatch.surface,
    "--preview-accent": swatch.accent,
    "--preview-text": swatch.text,
    "--preview-haze-a": swatch.hazeA,
    "--preview-haze-b": swatch.hazeB,
  } as CSSProperties;

  return (
    <label className={appearance.palette}>
      <input
        type="radio"
        name="appearance-palette"
        value={palette.id}
        checked={checked}
        onChange={onSelect}
        className={appearance.radio}
      />
      <span className={appearance.preview} style={previewStyle} aria-hidden="true">
        <span className={appearance.previewPanel}>
          <span className={appearance.previewLine} />
          <span className={appearance.previewLineShort} />
          <span className={appearance.previewButton} />
        </span>
      </span>
      <span className={appearance.paletteName}>{palette.label}</span>
      <span className={appearance.paletteDescription}>{palette.description}</span>
    </label>
  );
}
