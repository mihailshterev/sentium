import Layout from "../components/layout/layout";
import ProtectedRoute from "../components/protected-route";
import Dashboard from "../pages/dashboard/dashboard";
import Assistant from "../pages/assistant/assistant";
import AgentOrchestration from "../pages/orchestration/agent-orchestration";
import Agents from "../pages/agents/agents";
import Workflows from "../pages/workflows/workflows";
import System from "../pages/system/system";
import type { RouteObject } from "react-router";
import Login from "../pages/login/login";
import Workspaces from "../pages/workspaces/workspaces";
import Users from "../pages/users/users";
import Profile from "../pages/profile/profile";
import Watchdog from "../pages/watchdog/watchdog";
import Models from "../pages/models/models";
import SettingsPage from "../pages/settings/settings";
import KnowledgeBase from "../pages/knowledge-base/knowledge-base";
import Skills from "../pages/skills/skills";

export const routes: RouteObject[] = [
  {
    path: "/login",
    element: <Login />,
  },
  {
    path: "/",
    element: (
      <ProtectedRoute>
        <Layout />
      </ProtectedRoute>
    ),
    children: [
      { index: true, element: <Dashboard /> },
      { path: "profile", element: <Profile /> },
      { path: "watchdog", element: <Watchdog /> },
      { path: "workspaces", element: <Workspaces /> },
      { path: "assistant", element: <Assistant /> },
      { path: "orchestration", element: <AgentOrchestration /> },
      { path: "agents", element: <Agents /> },
      { path: "workflows", element: <Workflows /> },
      { path: "models", element: <Models /> },
      { path: "users", element: <Users /> },
      { path: "knowledge-base", element: <KnowledgeBase /> },
      { path: "skills", element: <Skills /> },
      { path: "system", element: <System /> },
      { path: "settings", element: <SettingsPage /> },
    ],
  },
];
