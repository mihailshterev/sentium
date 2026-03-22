import { BrowserRouter, Routes, Route } from "react-router";
import Layout from "./components/layout/layout";
import Home from "./pages/home";
import Agents from "./pages/agents";
import AgentOrchestration from "./components/agent-orchestration";

const App = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Home />} />
          <Route path="orchestration" element={<AgentOrchestration />} />
          <Route path="agents" element={<Agents />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
};

export default App;
