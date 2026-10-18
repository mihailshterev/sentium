import { create } from "zustand";
import { persist } from "zustand/middleware";

export type ThemePreference = "dark" | "light" | "system";
export type ResolvedTheme = "dark" | "light";

const colorSchemeQuery = (): MediaQueryList | null =>
  typeof window !== "undefined" && typeof window.matchMedia === "function"
    ? window.matchMedia("(prefers-color-scheme: dark)")
    : null;

export const resolveTheme = (preference: ThemePreference): ResolvedTheme => {
  if (preference !== "system") {
    return preference;
  }

  const mq = colorSchemeQuery();

  return mq?.matches ? "dark" : "light";
};

export const applyTheme = (theme: ResolvedTheme) => {
  if (typeof document !== "undefined") {
    document.documentElement.setAttribute("data-theme", theme);
  }
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
        const resolved = resolveTheme(preference);

        applyTheme(resolved);

        set({ preference });
      },

      toggle: () => {
        const currentResolved = resolveTheme(get().preference);

        const next: ResolvedTheme = currentResolved === "dark" ? "light" : "dark";

        applyTheme(next);

        set({
          preference: next,
        });
      },
    }),
    {
      name: "sentium-theme",

      partialize: (state) => ({
        preference: state.preference,
      }),

      onRehydrateStorage: () => (state) => {
        if (!state) return;

        applyTheme(resolveTheme(state.preference));
      },
    },
  ),
);

const mq = colorSchemeQuery();

mq?.addEventListener("change", () => {
  const { preference } = useThemeStore.getState();

  if (preference !== "system") {
    return;
  }

  applyTheme(resolveTheme("system"));
});
