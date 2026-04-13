import { Navigate } from "react-router";
import type { ReactNode } from "react";
import { useAuthStore } from "../stores/auth-store";
import { AUTH_STATUS } from "../utils/constants";

const ProtectedRoute = ({ children }: { children: ReactNode }) => {
  const status = useAuthStore((state) => state.status);

  if (status === AUTH_STATUS.IDLE || status === AUTH_STATUS.CHECKING) {
    return null;
  }

  if (status === AUTH_STATUS.UNAUTHENTICATED) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
};

export default ProtectedRoute;
