import { create } from "zustand";
import { BFF_BASE } from "../utils/constants";
import type { AuthStatus, User } from "../types/auth";

interface AuthState {
  user: User | null;
  status: AuthStatus;
  checkAuth: () => Promise<void>;
  login: (returnUrl?: string) => void;
  logout: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  status: "idle",

  checkAuth: async () => {
    set({ status: "checking" });
    try {
      const res = await fetch(`${BFF_BASE}/user`, {
        credentials: "include",
      });

      if (res.ok) {
        const data = await res.json();
        set({ user: data, status: "authenticated" });
      } else {
        set({ user: null, status: "unauthenticated" });
      }
    } catch {
      set({ user: null, status: "unauthenticated" });
    }
  },

  login: (returnUrl) => {
    const target = returnUrl ?? window.location.pathname;
    window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.origin + target)}`;
  },

  logout: async () => {
    try {
      await fetch(`${BFF_BASE}/logout`, {
        method: "POST",
        credentials: "include",
      });
    } finally {
      set({ user: null, status: "unauthenticated" });
    }
  },
}));
