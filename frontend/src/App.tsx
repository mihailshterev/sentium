import { BrowserRouter, Routes, Route } from "react-router";
import SentiumTerminal from "./components/agent-orchestration";
import Layout from "./components/layout/layout";
import Home from "./pages/home";
import Agents from "./pages/agents";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Home />} />
          <Route path="terminal" element={<SentiumTerminal />} />
          <Route path="agents" element={<Agents />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
