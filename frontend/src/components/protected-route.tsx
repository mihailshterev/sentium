import { Navigate } from "react-router";
import type { ReactNode } from "react";
import { useAuthStore } from "../stores/auth-store";

const ProtectedRoute = ({ children }: { children: ReactNode }) => {
  const status = useAuthStore((state) => state.status);

  if (status === "idle" || status === "checking") {
    return null;
  }

  if (status === "unauthenticated") {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
};

export default ProtectedRoute;
