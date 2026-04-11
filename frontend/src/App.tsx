import { useRoutes } from "react-router";
import { routes } from "./routes/routes";
import ConversationProvider from "./providers/conversation-provider";
import { useAuthStore } from "./stores/auth-store";
import { useEffect } from "react";

const App = () => {
  const checkAuth = useAuthStore((state) => state.checkAuth);
  const content = useRoutes(routes);

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  return <ConversationProvider>{content}</ConversationProvider>;
};

export default App;
