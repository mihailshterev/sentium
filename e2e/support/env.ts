import path from "node:path";

/**
 * Single source of truth for the e2e environment. Defaults describe the local, self-started stack
 * (compose.e2e.yaml + Sentium.Web in the Testing environment) and must stay in step with
 * src/hosts/Sentium.Web/appsettings.Testing.json.
 */

const repoRoot = path.resolve(import.meta.dirname, "..", "..");

export const isCI = !!process.env.CI;

/** Set PLAYWRIGHT_BASE_URL to run the suite against an already running stack; nothing is started then. */
export const externalBaseURL = process.env.PLAYWRIGHT_BASE_URL;

export const appPort = Number(process.env.E2E_APP_PORT ?? 5180);
export const baseURL = externalBaseURL ?? `http://localhost:${appPort}`;

/** The llama-server stand-in (support/llm-stub.ts); both inference endpoints point at it. */
export const llmStubPort = 21434;

/** Skip building the portal and Sentium.Web before starting it (CI builds in dedicated steps). */
export const skipBuild = process.env.E2E_SKIP_BUILD === "1";

export const paths = {
  repoRoot,
  composeFile: path.join(repoRoot, "e2e", "compose.e2e.yaml"),
  portal: path.join(repoRoot, "src", "clients", "sentium-portal"),
  webProject: path.join(repoRoot, "src", "hosts", "Sentium.Web", "Sentium.Web.csproj"),
  authState: path.join(repoRoot, "e2e", "playwright", ".auth", "user.json"),
};

/** Seeded by Sentium.Web in the Testing environment (E2E:* settings). */
export const testUser = {
  userName: process.env.E2E_USER_NAME ?? "testuser",
  password: process.env.E2E_USER_PASSWORD ?? "testuser1a",
};
