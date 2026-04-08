import Layout from "../components/layout/layout";
import Dashboard from "../pages/dashboard/dashboard";
import Assistant from "../pages/assistant/assistant";
import AgentOrchestration from "../components/agent-orchestration";
import Agents from "../pages/agents/agents";
import Workflows from "../pages/workflows/workflows";
import System from "../pages/system/system";
import Placeholder from "../pages/placeholder";
import type { RouteObject } from "react-router";

export const routes: RouteObject[] = [
  {
    path: "/",
    element: <Layout />,
    children: [
      { index: true, element: <Dashboard /> },
      { path: "sentinel", element: <Placeholder title="Sentinel" /> },
      { path: "watchdog", element: <Placeholder title="Watchdog" /> },
      { path: "assistant", element: <Assistant /> },
      { path: "orchestration", element: <AgentOrchestration /> },
      { path: "agents", element: <Agents /> },
      { path: "workflows", element: <Workflows /> },
      { path: "users", element: <Placeholder title="Users" /> },
      { path: "inventory", element: <Placeholder title="Inventory" /> },
      { path: "system", element: <System /> },
      { path: "settings", element: <Placeholder title="Settings" /> },
    ],
  },
];
