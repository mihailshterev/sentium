import { test as base } from "@playwright/test";
import {
  LoginPage,
  NavbarComponent,
  DashboardPage,
  AgentsPage,
  WorkflowsPage,
  AssistantPage,
  WorkspacesPage,
} from "../pages/index";

export type AppFixtures = {
  loginPage: LoginPage;
  navbar: NavbarComponent;
  dashboardPage: DashboardPage;
  agentsPage: AgentsPage;
  workflowsPage: WorkflowsPage;
  assistantPage: AssistantPage;
  workspacesPage: WorkspacesPage;
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
});

export { expect } from "@playwright/test";
