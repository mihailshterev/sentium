import Layout from "../layouts/layout";
import ProtectedRoute from "../guards/protected-route";
import Dashboard from "../../features/dashboard";
import Assistant from "../../features/assistant";
import AgentOrchestration from "../../features/orchestration";
import Agents from "../../features/agents";
import WorkflowsList from "../../features/workflows";
import WorkflowBuilder from "../../features/workflows/views/workflow-builder-view";
import System from "../../features/system";
import type { RouteObject } from "react-router";
import Workspaces from "../../features/workspaces";
import WorkspaceDetail from "../../features/workspaces/views/workspace-detail-view";
import Users from "../../features/users";
import Profile from "../../features/profile";
import Watchdog from "../../features/watchdog";
import Models from "../../features/models";
import SettingsPage from "../../features/registry";
import KnowledgeBase from "../../features/knowledge-base";
import Skills from "../../features/skills";
import Sentinel from "../../features/sentinel";
import Sandbox from "../../features/sandbox";
import SandboxRunDetail from "../../features/sandbox/views/sandbox-run-detail-view";
import SemanticMap from "../../features/semantic-map";
import Scheduler from "../../features/scheduler";

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
      { path: "sandbox/:jobId", element: <SandboxRunDetail /> },
      { path: "system", element: <System /> },
      { path: "settings", element: <SettingsPage /> },
    ],
  },
];
