import { createPersistedStore } from "@/shared/state/create-store";
import { DEFAULT_PALETTE, isPaletteId, type PaletteId } from "./palettes";

export type ThemePreference = "dark" | "light" | "system";
export type ResolvedTheme = "dark" | "light";

const THEME_ATTRIBUTE = "data-theme";
const PALETTE_ATTRIBUTE = "data-palette";

const colorSchemeQuery = (): MediaQueryList | null =>
  typeof window.matchMedia === "function" ? window.matchMedia("(prefers-color-scheme: dark)") : null;

export const resolveTheme = (preference: ThemePreference): ResolvedTheme => {
  if (preference !== "system") {
    return preference;
  }

  return colorSchemeQuery()?.matches ? "dark" : "light";
};

export const applyTheme = (theme: ResolvedTheme) => {
  document.documentElement.setAttribute(THEME_ATTRIBUTE, theme);
};

export const applyPalette = (palette: PaletteId) => {
  document.documentElement.setAttribute(PALETTE_ATTRIBUTE, palette);
};

/**
 * Calls `onChange` whenever the mode or palette on <html> changes, for canvas code that reads colours from the CSS
 * tokens and has to repaint. Returns the unsubscribe function.
 */
export function observeThemeChanges(onChange: () => void): () => void {
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: [THEME_ATTRIBUTE, PALETTE_ATTRIBUTE],
  });
  return () => observer.disconnect();
}

interface ThemeState {
  preference: ThemePreference;
  palette: PaletteId;
  setPreference: (preference: ThemePreference) => void;
  setPalette: (palette: PaletteId) => void;
  toggle: () => void;
}

export const useThemeStore = createPersistedStore<ThemeState, Pick<ThemeState, "preference" | "palette">>(
  (set, get) => ({
    preference: "system",
    palette: DEFAULT_PALETTE,

    setPreference: (preference) => {
      applyTheme(resolveTheme(preference));
      set({ preference });
    },

    setPalette: (palette) => {
      applyPalette(palette);
      set({ palette });
    },

    toggle: () => {
      const next: ResolvedTheme = resolveTheme(get().preference) === "dark" ? "light" : "dark";
      applyTheme(next);
      set({ preference: next });
    },
  }),
  {
    name: "sentium-theme",
    partialize: (state) => ({ preference: state.preference, palette: state.palette }),
    onRehydrate: (state) => {
      applyTheme(resolveTheme(state.preference));
      // A palette that was removed since it was stored falls back to the default.
      applyPalette(isPaletteId(state.palette) ? state.palette : DEFAULT_PALETTE);
    },
  },
);

if (!isPaletteId(useThemeStore.getState().palette)) {
  useThemeStore.setState({ palette: DEFAULT_PALETTE });
}

colorSchemeQuery()?.addEventListener("change", () => {
  if (useThemeStore.getState().preference === "system") {
    applyTheme(resolveTheme("system"));
  }
});
