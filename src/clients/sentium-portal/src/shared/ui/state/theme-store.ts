import { create } from "zustand";
import { persist } from "zustand/middleware";

export type ThemePreference = "dark" | "light" | "system";
export type ResolvedTheme = "dark" | "light";

const colorSchemeQuery = (): MediaQueryList | null =>
  typeof window.matchMedia === "function" ? window.matchMedia("(prefers-color-scheme: dark)") : null;

export const resolveTheme = (preference: ThemePreference): ResolvedTheme => {
  if (preference !== "system") {
    return preference;
  }

  return colorSchemeQuery()?.matches ? "dark" : "light";
};

export const applyTheme = (theme: ResolvedTheme) => {
  document.documentElement.setAttribute("data-theme", theme);
};

interface ThemeState {
  preference: ThemePreference;
  setPreference: (preference: ThemePreference) => void;
  toggle: () => void;
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      preference: "system",

      setPreference: (preference) => {
        applyTheme(resolveTheme(preference));
        set({ preference });
      },

      toggle: () => {
        const next: ResolvedTheme = resolveTheme(get().preference) === "dark" ? "light" : "dark";
        applyTheme(next);
        set({ preference: next });
      },
    }),
    {
      name: "sentium-theme",

      partialize: (state) => ({ preference: state.preference }),
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        applyTheme(resolveTheme(state.preference));
      },
    },
  ),
);

colorSchemeQuery()?.addEventListener("change", () => {
  if (useThemeStore.getState().preference === "system") {
    applyTheme(resolveTheme("system"));
  }
});
