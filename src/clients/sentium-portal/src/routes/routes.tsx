import Layout from "../components/layout/layout";
import ProtectedRoute from "../components/protected-route";
import Dashboard from "../pages/dashboard/dashboard";
import Assistant from "../pages/assistant/assistant";
import AgentOrchestration from "../pages/orchestration/agent-orchestration";
import Agents from "../pages/agents/agents";
import WorkflowsList from "../pages/workflows/workflows";
import WorkflowBuilder from "../pages/workflows/workflow-builder";
import System from "../pages/system/system";
import type { RouteObject } from "react-router";
import Workspaces from "../pages/workspaces/workspaces";
import WorkspaceDetail from "../pages/workspaces/workspace-detail";
import Users from "../pages/users/users";
import Profile from "../pages/profile/profile";
import Watchdog from "../pages/watchdog/watchdog";
import Models from "../pages/models/models";
import SettingsPage from "../pages/settings/settings";
import KnowledgeBase from "../pages/knowledge-base/knowledge-base";
import Skills from "../pages/skills/skills";
import Sentinel from "../pages/sentinel/sentinel";
import Sandbox from "../pages/sandbox/sandbox";
import SemanticMap from "../pages/semantic-map/semantic-map";
import Scheduler from "../pages/scheduler/scheduler";

export const routes: RouteObject[] = [
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
      { path: "workspaces/:workspaceId", element: <WorkspaceDetail /> },
      { path: "assistant", element: <Assistant /> },
      { path: "assistant/:conversationId", element: <Assistant /> },
      { path: "orchestration", element: <AgentOrchestration /> },
      { path: "orchestration/runs/:runId", element: <AgentOrchestration /> },
      { path: "agents", element: <Agents /> },
      { path: "workflows", element: <WorkflowsList /> },
      { path: "workflows/new", element: <WorkflowBuilder /> },
      { path: "workflows/:workflowId", element: <WorkflowBuilder /> },
      { path: "models", element: <Models /> },
      { path: "users", element: <Users /> },
      { path: "knowledge-base", element: <KnowledgeBase /> },
      { path: "semantic-map", element: <SemanticMap /> },
      { path: "scheduler", element: <Scheduler /> },
      { path: "skills", element: <Skills /> },
      { path: "sentinel", element: <Sentinel /> },
      { path: "sandbox", element: <Sandbox /> },
      { path: "system", element: <System /> },
      { path: "settings", element: <SettingsPage /> },
    ],
  },
];
