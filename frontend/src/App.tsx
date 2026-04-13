import { useRoutes } from "react-router";
import { routes } from "./routes/routes";
import ConversationProvider from "./providers/conversation-provider";
import { useAuthStore } from "./stores/auth-store";
import { useEffect } from "react";
import { AUTH_STATUS } from "./utils/constants";
import { Loader2 } from "lucide-react";

const App = () => {
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
          background: "#0a0a0a",
        }}
      >
        <Loader2 className="animate-spin" size={48} color="#00f2ff" />
      </div>
    );
  }

  return <ConversationProvider>{content}</ConversationProvider>;
};

export default App;
