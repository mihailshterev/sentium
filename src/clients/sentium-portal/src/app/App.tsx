import { useRoutes } from "react-router";
import { routes } from "./routes/routes";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { useEffect } from "react";
import { Loader2 } from "lucide-react";
import { AUTH_STATUS } from "@/shared/utils/constants";

export function App() {
  const { checkAuth, status } = useAuthStore();
  const content = useRoutes(routes);

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  if (status === AUTH_STATUS.IDLE || status === AUTH_STATUS.CHECKING) {
    return (
      <div
        style={{
          height: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: "var(--bg-base)",
        }}
      >
        <Loader2 className="animate-spin" size={48} color="var(--accent)" />
      </div>
    );
  }

  return <>{content}</>;
}
