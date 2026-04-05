import { useRoutes } from "react-router";
import { routes } from "./routes/routes";
import ConversationProvider from "./providers/conversation-provider";

const App = () => {
  const content = useRoutes(routes);
  return <ConversationProvider>{content}</ConversationProvider>;
};

export default App;
