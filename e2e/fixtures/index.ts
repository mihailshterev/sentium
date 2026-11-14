import { test as base, expect } from "@playwright/test";
import {
  LoginPage,
  NavbarComponent,
  DashboardPage,
  AgentsPage,
  WorkflowsPage,
  AssistantPage,
  WorkspacesPage,
  InventoryPage,
  SettingsPage,
  ProfilePage,
  OrchestrationPage,
  KnowledgeBasePage,
  SkillsPage,
  ModelsPage,
  SandboxPage,
  SchedulerPage,
  SentinelPage,
  VigilPage,
  SystemPage,
  UsersPage,
  SemanticMapPage,
  TimelinePage,
} from "../pages/index";

export type AppFixtures = {
  ownOriginOnly: void;
  loginPage: LoginPage;
  navbar: NavbarComponent;
  dashboardPage: DashboardPage;
  agentsPage: AgentsPage;
  workflowsPage: WorkflowsPage;
  assistantPage: AssistantPage;
  workspacesPage: WorkspacesPage;
  inventoryPage: InventoryPage;
  settingsPage: SettingsPage;
  profilePage: ProfilePage;
  orchestrationPage: OrchestrationPage;
  knowledgeBasePage: KnowledgeBasePage;
  skillsPage: SkillsPage;
  modelsPage: ModelsPage;
  sandboxPage: SandboxPage;
  schedulerPage: SchedulerPage;
  sentinelPage: SentinelPage;
  vigilPage: VigilPage;
  systemPage: SystemPage;
  usersPage: UsersPage;
  semanticMapPage: SemanticMapPage;
  timelinePage: TimelinePage;
};

export const test = base.extend<AppFixtures>({
  // Runs for every test: the portal must make no request beyond its own origin, and nothing it does may trip the
  // Content-Security-Policy (a violation means a feature silently broke under the policy).
  ownOriginOnly: [
    async ({ page, baseURL, browserName }, use) => {
      const origin = new URL(baseURL!).origin;
      const problems: string[] = [];

      page.on("request", (request) => {
        const url = new URL(request.url());
        if (url.protocol.startsWith("http") && url.origin !== origin) {
          problems.push(`request to another origin: ${request.url()}`);
        }
      });

      await page.exposeFunction("__reportCspViolation", (directive: string, blocked: string, source: string) => {
        problems.push(`CSP violation: ${directive} blocked ${blocked || "inline content"} (${source})`);
      });
      // WebKit reports engine artifacts that are not portal code: its own user-agent shadow DOM (a <select>'s
      // internals, sometimes reported after the element is gone) and the "body {}" style Playwright's
      // web-inspector bootstrap injects. Chromium and Firefox stay fully strict and run the same portal code.
      await page.addInitScript((isWebKit) => {
        document.addEventListener("securitypolicyviolation", (event) => {
          if (isWebKit && (event.violatedDirective === "style-src-attr" || event.sourceFile === "")) {
            return;
          }

          const report = (window as unknown as { __reportCspViolation: (...args: string[]) => void })
            .__reportCspViolation;
          void report(event.violatedDirective, event.blockedURI, event.sourceFile);
        });
      }, browserName === "webkit");

      await use();

      expect(problems, "the portal must stay on its own origin and within its CSP").toEqual([]);
    },
    { auto: true },
  ],

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

  inventoryPage: async ({ page }, use) => {
    await use(new InventoryPage(page));
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

  vigilPage: async ({ page }, use) => {
    await use(new VigilPage(page));
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

  timelinePage: async ({ page }, use) => {
    await use(new TimelinePage(page));
  },
});

export { expect } from "@playwright/test";
