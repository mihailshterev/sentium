import { createPersistedStore } from "@/shared/state/create-store";

interface UiState {
  /** User's choice for the sidebar; narrow windows force the rail regardless. */
  sidebarCollapsed: boolean;
  /** Ids of the navigation groups the user folded away. */
  collapsedNavGroups: string[];
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  toggleNavGroup: (groupId: string) => void;
}

type PersistedUiState = Pick<UiState, "sidebarCollapsed" | "collapsedNavGroups">;

export const useUiStore = createPersistedStore<UiState, PersistedUiState>(
  (set) => ({
    sidebarCollapsed: false,
    collapsedNavGroups: [],
    toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
    setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
    toggleNavGroup: (groupId) =>
      set((s) => ({
        collapsedNavGroups: s.collapsedNavGroups.includes(groupId)
          ? s.collapsedNavGroups.filter((id) => id !== groupId)
          : [...s.collapsedNavGroups, groupId],
      })),
  }),
  {
    name: "sentium-ui",
    partialize: (state) => ({
      sidebarCollapsed: state.sidebarCollapsed,
      collapsedNavGroups: state.collapsedNavGroups,
    }),
  },
);
