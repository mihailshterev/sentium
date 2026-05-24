import { useEffect, type ReactNode } from "react";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

const ProtectedRoute = ({ children }: { children: ReactNode }) => {
  const { status, login } = useAuthStore();

  useEffect(() => {
    if (status === AUTH_STATUS.UNAUTHENTICATED) {
      login(window.location.pathname);
    }
  }, [status, login]);

  if (status === AUTH_STATUS.UNAUTHENTICATED || status === AUTH_STATUS.IDLE || status === AUTH_STATUS.CHECKING) {
    return null;
  }

  return <>{children}</>;
};

export default ProtectedRoute;
