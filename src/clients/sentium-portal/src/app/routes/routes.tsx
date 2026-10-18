import { Layout } from "../layouts/layout";
import { ProtectedRoute } from "../guards/protected-route";
import { DashboardView as Dashboard } from "../../features/dashboard";
import { AssistantView as Assistant } from "../../features/assistant";
import { AgentOrchestrationView as AgentOrchestration } from "../../features/orchestration";
import { AgentsView as Agents } from "../../features/agents";
import { WorkflowsView as WorkflowsList } from "../../features/workflows";
import { WorkflowBuilderView as WorkflowBuilder } from "../../features/workflows/views/workflow-builder-view";
import { SystemView as System } from "../../features/system";
import type { RouteObject } from "react-router";
import { WorkspacesView as Workspaces } from "../../features/workspaces";
import { WorkspaceDetailView as WorkspaceDetail } from "../../features/workspaces/views/workspace-detail-view";
import { UsersView as Users } from "../../features/users";
import { ProfileView as Profile } from "../../features/profile";
import { WatchdogView as Watchdog } from "../../features/watchdog";
import { ModelsView as Models } from "../../features/models";
import { SettingsView as SettingsPage } from "../../features/registry";
import { KnowledgeBaseView as KnowledgeBase } from "../../features/knowledge-base";
import { SkillsView as Skills } from "../../features/skills";
import { SentinelView as Sentinel } from "../../features/sentinel";
import { SandboxView as Sandbox } from "../../features/sandbox";
import { SandboxRunDetailView as SandboxRunDetail } from "../../features/sandbox/views/sandbox-run-detail-view";
import { SemanticMapView as SemanticMap } from "../../features/semantic-map";
import { SchedulerView as Scheduler } from "../../features/scheduler";

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
