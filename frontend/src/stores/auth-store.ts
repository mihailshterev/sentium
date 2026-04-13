import { create } from "zustand";
import type { AuthStatus, User } from "../types/auth";
import { BFF_BASE } from "../api/client";
import { AUTH_STATUS } from "../utils/constants";

interface AuthState {
  user: User | null;
  status: AuthStatus;
  checkAuth: () => Promise<void>;
  login: (returnUrl?: string) => void;
  logout: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  status: AUTH_STATUS.IDLE,

  checkAuth: async () => {
    set({ status: AUTH_STATUS.CHECKING });
    try {
      const res = await fetch(`${BFF_BASE}/user`, {
        credentials: "include",
      });

      if (res.ok) {
        const data = await res.json();
        set({ user: data, status: AUTH_STATUS.AUTHENTICATED });
      } else {
        set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
      }
    } catch {
      set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
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
      set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
    }
  },
}));
