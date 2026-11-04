import { AUTH_STATUS } from "@/shared/utils/constants";
import { useAuthStore } from "./state/auth-store";

export const handleUnauthorized = (): never => {
  useAuthStore.setState({ user: null, status: AUTH_STATUS.UNAUTHENTICATED });

  if (!window.location.pathname.includes("/login")) {
    window.location.href = `/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
  }

  throw new Error("Session expired. Please log in again.");
};
