import { create } from "zustand";
import type { AuthStatus, User } from "../types/auth";
import { BFF_BASE } from "../api/client";
import { AUTH_STATUS } from "../utils/constants";

interface AuthState {
  user: User | null;
  status: AuthStatus;
  checkAuth: () => Promise<void>;
  login: (returnUrl?: string) => void;
  logout: () => void;
  updateUser: (patch: Partial<Pick<User, "name" | "email">>) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  status: AUTH_STATUS.IDLE,

  checkAuth: async () => {
    set({ status: AUTH_STATUS.CHECKING });

    const maxAttempts = 3;
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        const res = await fetch(`${BFF_BASE}/user`, {
          credentials: "include",
        });

        if (res.ok) {
          const data = await res.json();
          set({ user: data, status: AUTH_STATUS.AUTHENTICATED });
          return;
        }

        if (res.status === 401 || res.status === 403) {
          set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
          return;
        }
      } catch {
        //
      }

      if (attempt < maxAttempts) {
        await new Promise((resolve) => setTimeout(resolve, 300 * attempt));
      }
    }

    set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
  },

  login: (returnUrl) => {
    const target = returnUrl ?? window.location.pathname;
    window.location.href = `${BFF_BASE}/login?returnUrl=${encodeURIComponent(window.location.origin + target)}`;
  },

  updateUser: (patch) => {
    set((state) => (state.user ? { user: { ...state.user, ...patch } } : {}));
  },

  logout: async () => {
    set({ user: null, status: AUTH_STATUS.CHECKING });
    try {
      const res = await fetch(`${BFF_BASE}/logout`, {
        method: "POST",
        credentials: "include",
      });

      if (res.ok) {
        window.location.href = res.url;
      } else {
        set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
      }
    } catch {
      set({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });
    }
  },
}));
