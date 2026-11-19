import { createPersistedStore } from "@/shared/state/create-store";

interface UiState {
  /** User's choice for the sidebar; narrow windows force the rail regardless. */
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
}

export const useUiStore = createPersistedStore<UiState, Pick<UiState, "sidebarCollapsed">>(
  (set) => ({
    sidebarCollapsed: false,
    toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
    setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
  }),
  {
    name: "sentium-ui",
    partialize: (state) => ({ sidebarCollapsed: state.sidebarCollapsed }),
  },
);
