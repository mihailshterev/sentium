import Layout from "../layouts/layout";
import ProtectedRoute from "../guards/protected-route";
import Dashboard from "../../features/dashboard/dashboard";
import Assistant from "../../features/assistant/assistant";
import AgentOrchestration from "../../features/orchestration/agent-orchestration";
import Agents from "../../features/agents/agents";
import WorkflowsList from "../../features/workflows/workflows";
import WorkflowBuilder from "../../features/workflows/workflow-builder";
import System from "../../features/system/system";
import type { RouteObject } from "react-router";
import Workspaces from "../../features/workspaces/workspaces";
import WorkspaceDetail from "../../features/workspaces/workspace-detail";
import Users from "../../features/users/users";
import Profile from "../../features/profile/profile";
import Watchdog from "../../features/watchdog/watchdog";
import Models from "../../features/models/models";
import SettingsPage from "../../features/registry/settings";
import KnowledgeBase from "../../features/knowledge-base/knowledge-base";
import Skills from "../../features/skills/skills";
import Sentinel from "../../features/sentinel/sentinel";
import Sandbox from "../../features/sandbox/sandbox";
import SandboxRunDetail from "../../features/sandbox/sandbox-run-detail";
import SemanticMap from "../../features/semantic-map/semantic-map";
import Scheduler from "../../features/scheduler/scheduler";

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
