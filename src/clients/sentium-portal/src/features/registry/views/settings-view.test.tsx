import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SettingsView } from "./settings-view";
import * as registryService from "../api/registry.service";
import * as modelsHook from "@/features/models/hooks/useModels";
import * as useRoleHook from "@/shared/auth/hooks/useRole";
import type { SettingsKey } from "../types/settings";

const VALUES: Record<SettingsKey, Record<string, unknown>> = {
  harness: { userHarnessPrompt: "Be concise.", isBuiltInHarnessEnabled: true, isPromptEnhancementEnabled: false },
  "calendar-user": { timeZone: null, dayStart: null },
  inference: { defaultModel: "gemma-4-e4b", agentTemperature: 0.3 },
  calendar: { timeZone: "Europe/Sofia", dayStart: "08:00:00" },
  pdp: {
    lockdownMode: false,
    autonomyLevel: 5,
    semanticIntentCheckEnabled: true,
    intentCheckModel: "",
    rateLimitMaxRequests: 120,
    rateLimitWindowSeconds: 60,
  },
  mcp: {
    enabled: true,
    connectTimeoutSeconds: 30,
    callTimeoutSeconds: 60,
    idleTimeoutMinutes: 15,
    maxResultChars: 16000,
  },
  vigil: {
    pollIntervalSeconds: 15,
    probeTimeoutSeconds: 5,
    degradedLatencyMs: 1000,
    consecutiveFailuresToOpenIncident: 3,
    sampleHistorySize: 60,
  },
};

const envelope = (key: SettingsKey, value: Record<string, unknown> = VALUES[key]) => ({
  key,
  value,
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: key === "harness" ? "alice" : null,
});

const role = (isSovereign: boolean) =>
  ({ isSovereign, roles: [], isMemberOrAbove: true, isAuthenticated: true }) as unknown as ReturnType<
    typeof useRoleHook.useRole
  >;

let fetchGroup: ReturnType<typeof spyFetch>;
let updateGroup: ReturnType<typeof spyUpdate>;
const spyFetch = () => vi.spyOn(registryService, "fetchSettingsGroup");
const spyUpdate = () => vi.spyOn(registryService, "updateSettingsGroup");

const renderSettings = (path = "/settings") => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/settings/:section?" element={<SettingsView />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

const loaded = () => screen.findByRole("heading", { level: 2, name: "Agents" });

beforeEach(() => {
  Element.prototype.scrollIntoView = vi.fn();
  vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(false));
  vi.spyOn(modelsHook, "useModels").mockReturnValue({ models: ["gemma-4-e4b", "qwen"], defaultModel: "gemma-4-e4b" });
  fetchGroup = spyFetch().mockImplementation(async (key) => envelope(key) as never);
  updateGroup = spyUpdate().mockImplementation(
    async (key, patch) => envelope(key, { ...VALUES[key], ...patch }) as never,
  );
});

describe("Settings layout", () => {
  it("shows a loading state until the groups arrive", () => {
    fetchGroup.mockImplementation(() => new Promise(() => {}));
    renderSettings();
    expect(screen.getByText(/loading settings/i)).toBeInTheDocument();
  });

  it("shows only personal sections, and fetches only personal groups, to non-Sovereigns", async () => {
    renderSettings();
    await loaded();

    expect(screen.getByRole("heading", { level: 2, name: "Calendar" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2, name: "Security" })).not.toBeInTheDocument();
    expect(fetchGroup.mock.calls.map(([key]) => key).sort()).toEqual(["calendar-user", "harness"]);
  });

  it("adds the household sections for a Sovereign, including settings that used to live on feature pages", async () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    renderSettings();
    await loaded();

    for (const name of ["Inference", "Household calendar", "Security", "MCP", "Monitoring"]) {
      expect(screen.getByRole("heading", { level: 2, name })).toBeInTheDocument();
    }
    expect(screen.getByRole("option", { name: "Household default (Europe/Sofia)" })).toBeInTheDocument();
    expect(screen.getByTestId("semantic-intent-toggle")).toBeChecked();
    expect(screen.getByTestId("autonomy-slider")).toHaveValue("5");
    expect(screen.getByLabelText("Intent check model")).toHaveValue("");
    expect(screen.getByRole("option", { name: "Default model" })).toBeInTheDocument();
  });

  it("shows who last saved a group", async () => {
    renderSettings();
    await loaded();
    expect(screen.getByText(/last saved by alice/i)).toBeInTheDocument();
  });
});

describe("Settings editing", () => {
  it("shows the save bar only once something changed", async () => {
    renderSettings();
    await loaded();
    expect(screen.queryByRole("button", { name: /save changes/i })).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Global prompt"), { target: { value: "New instruction." } });

    expect(screen.getByText("1 unsaved change")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /save changes/i })).toBeEnabled();
  });

  it("drops a change that is reverted by hand", async () => {
    renderSettings();
    await loaded();
    const prompt = screen.getByLabelText("Global prompt");

    fireEvent.change(prompt, { target: { value: "Other." } });
    fireEvent.change(prompt, { target: { value: "Be concise." } });

    expect(screen.queryByRole("button", { name: /save changes/i })).not.toBeInTheDocument();
  });

  it("saves only the changed fields of the changed groups", async () => {
    renderSettings();
    await loaded();

    fireEvent.click(screen.getByRole("switch", { name: "Prompt enhancement" }));
    fireEvent.change(screen.getByLabelText("Time zone"), { target: { value: "Europe/Sofia" } });
    fireEvent.change(screen.getByLabelText("Day starts at"), { target: { value: "07:30" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await screen.findByText(/settings successfully saved/i);
    expect(updateGroup).toHaveBeenCalledTimes(2);
    expect(updateGroup).toHaveBeenCalledWith("harness", { isPromptEnhancementEnabled: true });
    expect(updateGroup).toHaveBeenCalledWith("calendar-user", { timeZone: "Europe/Sofia", dayStart: "07:30:00" });
  });

  it("keeps the changes of a group that failed and names it", async () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    updateGroup.mockImplementation(async (key, patch) => {
      if (key === "mcp") {
        throw new Error("Validation failed");
      }
      return envelope(key, { ...VALUES[key], ...patch }) as never;
    });
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByLabelText("Global prompt"), { target: { value: "Saved." } });
    fireEvent.change(screen.getByLabelText("Call timeout"), { target: { value: "90" } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await screen.findByText(/failed to save mcp/i);
    expect(screen.getByText(/not saved: validation failed/i)).toBeInTheDocument();
    expect(screen.getByLabelText("Call timeout")).toHaveValue(90);
    expect(screen.getByLabelText("Global prompt")).toHaveValue("Saved.");
  });

  it("discards every unsaved change", async () => {
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByLabelText("Global prompt"), { target: { value: "Draft." } });
    fireEvent.click(screen.getByRole("button", { name: "Discard" }));

    expect(screen.getByLabelText("Global prompt")).toHaveValue("Be concise.");
    expect(screen.queryByRole("button", { name: /save changes/i })).not.toBeInTheDocument();
  });

  it("resets a modified setting to its default", async () => {
    renderSettings();
    await loaded();

    fireEvent.click(screen.getByRole("button", { name: "Reset Global prompt to default" }));

    expect(screen.getByLabelText("Global prompt")).toHaveValue("");
    expect(screen.getByText("1 unsaved change")).toBeInTheDocument();
  });
});

describe("Settings validation", () => {
  it("blocks saving a prompt over the limit", async () => {
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByLabelText("Global prompt"), { target: { value: "a".repeat(16001) } });

    expect(screen.getByText(/1 characters over limit/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /save changes/i })).toBeDisabled();
  });

  it("checks rules that span settings", async () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByLabelText("Probe timeout"), { target: { value: "30" } });

    expect(screen.getByText("Must not exceed the poll interval.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /save changes/i })).toBeDisabled();
  });

  it("checks number bounds", async () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByLabelText("Connect timeout"), { target: { value: "0" } });

    expect(screen.getByText("Must be between 1 and 300.")).toBeInTheDocument();
  });
});

describe("Settings search and filters", () => {
  it("filters settings by search terms, including keywords", async () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(role(true));
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByRole("searchbox", { name: "Search settings" }), { target: { value: "timeout" } });

    expect(screen.getByLabelText("Call timeout")).toBeInTheDocument();
    expect(screen.getByLabelText("Probe timeout")).toBeInTheDocument();
    expect(screen.queryByLabelText("Global prompt")).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2, name: "Agents" })).not.toBeInTheDocument();
  });

  it("shows an empty state when nothing matches", async () => {
    renderSettings();
    await loaded();

    fireEvent.change(screen.getByRole("searchbox", { name: "Search settings" }), { target: { value: "zzz" } });

    expect(screen.getByText("No settings match")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(screen.getByRole("heading", { level: 2, name: "Agents" })).toBeInTheDocument();
  });

  it("shows only settings that differ from their default", async () => {
    renderSettings();
    await loaded();

    fireEvent.click(screen.getByRole("button", { name: /modified only/i }));

    const agents = screen.getByRole("region", { name: "Agents" });
    expect(within(agents).getByLabelText("Global prompt")).toBeInTheDocument();
    expect(within(agents).queryByRole("switch", { name: "Prompt enhancement" })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 2, name: "Calendar" })).not.toBeInTheDocument();
  });

  it("scrolls to the section named in the URL", async () => {
    renderSettings("/settings/calendar");
    await loaded();
    await waitFor(() => expect(Element.prototype.scrollIntoView).toHaveBeenCalled());
  });
});
