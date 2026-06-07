import { test as base } from "@playwright/test";
import {
  LoginPage,
  NavbarComponent,
  DashboardPage,
  AgentsPage,
  WorkflowsPage,
  AssistantPage,
  WorkspacesPage,
  SettingsPage,
  ProfilePage,
  OrchestrationPage,
  KnowledgeBasePage,
  SkillsPage,
  ModelsPage,
  SandboxPage,
  SchedulerPage,
  SentinelPage,
  WatchdogPage,
  SystemPage,
  UsersPage,
  SemanticMapPage,
} from "../pages/index";

export type AppFixtures = {
  loginPage: LoginPage;
  navbar: NavbarComponent;
  dashboardPage: DashboardPage;
  agentsPage: AgentsPage;
  workflowsPage: WorkflowsPage;
  assistantPage: AssistantPage;
  workspacesPage: WorkspacesPage;
  settingsPage: SettingsPage;
  profilePage: ProfilePage;
  orchestrationPage: OrchestrationPage;
  knowledgeBasePage: KnowledgeBasePage;
  skillsPage: SkillsPage;
  modelsPage: ModelsPage;
  sandboxPage: SandboxPage;
  schedulerPage: SchedulerPage;
  sentinelPage: SentinelPage;
  watchdogPage: WatchdogPage;
  systemPage: SystemPage;
  usersPage: UsersPage;
  semanticMapPage: SemanticMapPage;
};

export const test = base.extend<AppFixtures>({
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },

  navbar: async ({ page }, use) => {
    await use(new NavbarComponent(page));
  },

  dashboardPage: async ({ page }, use) => {
    await use(new DashboardPage(page));
  },

  agentsPage: async ({ page }, use) => {
    await use(new AgentsPage(page));
  },

  workflowsPage: async ({ page }, use) => {
    await use(new WorkflowsPage(page));
  },

  assistantPage: async ({ page }, use) => {
    await use(new AssistantPage(page));
  },

  workspacesPage: async ({ page }, use) => {
    await use(new WorkspacesPage(page));
  },

  settingsPage: async ({ page }, use) => {
    await use(new SettingsPage(page));
  },

  profilePage: async ({ page }, use) => {
    await use(new ProfilePage(page));
  },

  orchestrationPage: async ({ page }, use) => {
    await use(new OrchestrationPage(page));
  },

  knowledgeBasePage: async ({ page }, use) => {
    await use(new KnowledgeBasePage(page));
  },

  skillsPage: async ({ page }, use) => {
    await use(new SkillsPage(page));
  },

  modelsPage: async ({ page }, use) => {
    await use(new ModelsPage(page));
  },

  sandboxPage: async ({ page }, use) => {
    await use(new SandboxPage(page));
  },

  schedulerPage: async ({ page }, use) => {
    await use(new SchedulerPage(page));
  },

  sentinelPage: async ({ page }, use) => {
    await use(new SentinelPage(page));
  },

  watchdogPage: async ({ page }, use) => {
    await use(new WatchdogPage(page));
  },

  systemPage: async ({ page }, use) => {
    await use(new SystemPage(page));
  },

  usersPage: async ({ page }, use) => {
    await use(new UsersPage(page));
  },

  semanticMapPage: async ({ page }, use) => {
    await use(new SemanticMapPage(page));
  },
});

export { expect } from "@playwright/test";
