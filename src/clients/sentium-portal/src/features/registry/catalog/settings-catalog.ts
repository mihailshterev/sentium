import { Activity, Bot, CalendarClock, Cpu, Home, Plug, ShieldCheck } from "lucide-react";
import type { SettingDefinition, SettingScope, SettingsSection, SettingValue } from "../types/settings";

export const PROMPT_LIMIT = 16000;

/** Keep in sync with `AIModels.Gemma4_E4B`, the server's default model. */
const DEFAULT_MODEL = "gemma-4-e4b";

/** `HH:mm:ss` from the server to the `HH:mm` of a time input; null and empty mean "not set". */
const toTimeInput = (value: unknown) => (typeof value === "string" && value ? value.slice(0, 5) : "");
const fromTimeInput = (value: SettingValue) => (value ? `${String(value)}:00` : null);

const nullToEmpty = (value: unknown) => (typeof value === "string" ? value : "");
const emptyToNull = (value: SettingValue) => (value === "" ? null : value);

const roundTo = (step: number) => (value: unknown) => Math.round(Number(value) / step) * step;

const autonomyName = (level: number) =>
  level <= 2 ? "Max security" : level >= 9 ? "Max autonomy" : `Level ${level}`;

const autonomyEffect = (level: number) =>
  level <= 2
    ? "Inconclusive intent checks are treated as denials."
    : level >= 9
      ? "The semantic intent check is bypassed entirely."
      : "Semantic checks run normally; an inconclusive check allows the action.";

const setting = (
  group: SettingDefinition["group"],
  field: string,
  rest: Omit<SettingDefinition, "id" | "group" | "field">,
) => ({ id: `${group}.${field}`, group, field, ...rest }) satisfies SettingDefinition;

export const SCOPES: { id: SettingScope; title: string; description: string }[] = [
  { id: "personal", title: "Personal", description: "Only affects you" },
  { id: "household", title: "Household", description: "Applies to everyone · Sovereign only" },
];

export const SETTINGS_SECTIONS: SettingsSection[] = [
  {
    id: "agents",
    scope: "personal",
    group: "harness",
    title: "Agents",
    description: "How every agent you run is instructed.",
    icon: Bot,
    settings: [
      setting("harness", "isBuiltInHarnessEnabled", {
        title: "Built-in governance harness",
        description:
          "Prepends the Universal Agent Governance policy (chain-of-thought, retrieval-first, anti-hallucination) to every agent interaction. Recommended to keep on.",
        keywords: ["policy", "safety", "hallucination", "system prompt"],
        control: { kind: "switch" },
        defaultValue: true,
      }),
      setting("harness", "isPromptEnhancementEnabled", {
        title: "Prompt enhancement",
        description:
          "Rewrites assistant and workflow prompts for clarity and specificity before the agent runs, for sharper results from smaller local models. The original is kept and the enhanced version is shown inline.",
        keywords: ["rewrite", "optimize", "pre-pass"],
        control: { kind: "switch" },
        defaultValue: false,
      }),
      setting("harness", "userHarnessPrompt", {
        title: "Global prompt",
        description:
          "Injected into every agent alongside the built-in harness: personas, tone, domain focus or operational constraints. Plain text or markdown.",
        keywords: ["persona", "tone", "instructions", "behavior", "system prompt"],
        control: {
          kind: "textarea",
          maxLength: PROMPT_LIMIT,
          rows: 8,
          placeholder:
            'Examples:\n- "Always respond in a professional, concise tone."\n- "You are a local AI orchestrator. Prioritize efficient workflow execution."',
        },
        defaultValue: "",
      }),
    ],
  },
  {
    id: "calendar",
    scope: "personal",
    group: "calendar-user",
    title: "Calendar",
    description:
      "When your day turns over: events count as past after your midnight, and reminders for entries without a time fire at your day start.",
    icon: CalendarClock,
    settings: [
      setting("calendar-user", "timeZone", {
        title: "Time zone",
        description: "Your time zone for the timeline and reminders.",
        keywords: ["timezone", "tz", "region", "clock"],
        control: { kind: "timezone", inherit: true },
        defaultValue: "",
        fromServer: nullToEmpty,
        toServer: emptyToNull,
      }),
      setting("calendar-user", "dayStart", {
        title: "Day starts at",
        description: "Leave empty to use the household's.",
        keywords: ["morning", "reminder", "time"],
        control: { kind: "time", required: false },
        defaultValue: "",
        fromServer: toTimeInput,
        toServer: fromTimeInput,
      }),
    ],
  },
  {
    id: "inference",
    scope: "household",
    group: "inference",
    title: "Inference",
    description:
      "Model and sampling parameters for every agent; they apply from the next agent call. Server endpoints and per-model context size are deployment configuration.",
    icon: Cpu,
    settings: [
      setting("inference", "defaultModel", {
        title: "Default model",
        description: "The chat model agents use unless they pick their own.",
        keywords: ["llm", "model", "llama"],
        control: { kind: "model" },
        defaultValue: DEFAULT_MODEL,
      }),
      setting("inference", "agentTemperature", {
        title: "Temperature",
        description: "Lower is more precise and repeatable, higher is more creative.",
        keywords: ["sampling", "creativity", "randomness"],
        control: {
          kind: "range",
          min: 0,
          max: 1,
          step: 0.05,
          minLabel: "Precise (0.0)",
          maxLabel: "Creative (1.0)",
          format: (v) => v.toFixed(2),
        },
        defaultValue: 0.3,
        fromServer: roundTo(0.01),
      }),
    ],
  },
  {
    id: "household-calendar",
    scope: "household",
    group: "calendar",
    title: "Household calendar",
    description: "Defaults for everyone who hasn't set their own time zone or day start.",
    icon: Home,
    settings: [
      setting("calendar", "timeZone", {
        title: "Time zone",
        description: "The household's time zone.",
        keywords: ["timezone", "tz", "region", "clock"],
        control: { kind: "timezone", inherit: false },
        defaultValue: "UTC",
      }),
      setting("calendar", "dayStart", {
        title: "Day starts at",
        description: "When reminders for entries without a time fire.",
        keywords: ["morning", "reminder", "time"],
        control: { kind: "time", required: true },
        defaultValue: "08:00",
        fromServer: toTimeInput,
        toServer: fromTimeInput,
      }),
    ],
  },
  {
    id: "security",
    scope: "household",
    group: "pdp",
    title: "Security",
    description: "How Sentinel's policy decision point judges every agent action.",
    icon: ShieldCheck,
    note: {
      text: "Lockdown mode is an emergency switch and lives on the Sentinel page.",
      linkLabel: "Open Sentinel",
      to: "/sentinel",
    },
    settings: [
      setting("pdp", "semanticIntentCheckEnabled", {
        title: "Semantic intent check",
        description: "Asks a model to verify that each agent action matches what the user asked for.",
        keywords: ["sentinel", "pdp", "alignment", "llm"],
        control: { kind: "switch" },
        defaultValue: true,
        testId: "semantic-intent-toggle",
      }),
      setting("pdp", "intentCheckModel", {
        title: "Intent check model",
        description: "The model that runs the semantic intent check. Empty uses the default model.",
        keywords: ["sentinel", "pdp", "llm"],
        control: { kind: "model", emptyLabel: "Default model" },
        defaultValue: "",
      }),
      setting("pdp", "autonomyLevel", {
        title: "Autonomy level",
        description: "Trades security for agent autonomy when the intent check is inconclusive.",
        keywords: ["sentinel", "pdp", "security", "trust"],
        control: {
          kind: "range",
          min: 1,
          max: 10,
          step: 1,
          minLabel: "High security",
          maxLabel: "High autonomy",
          format: autonomyName,
          explain: autonomyEffect,
        },
        defaultValue: 5,
        testId: "autonomy-slider",
      }),
      setting("pdp", "rateLimitMaxRequests", {
        title: "Rate limit: requests",
        description: "Agent actions allowed per window before Sentinel denies further ones.",
        keywords: ["sentinel", "pdp", "throttle", "quota"],
        control: { kind: "number", min: 1, unit: "requests" },
        defaultValue: 120,
      }),
      setting("pdp", "rateLimitWindowSeconds", {
        title: "Rate limit: window",
        description: "The window the request limit is counted over.",
        keywords: ["sentinel", "pdp", "throttle", "quota"],
        control: { kind: "number", min: 1, unit: "s" },
        defaultValue: 60,
      }),
    ],
  },
  {
    id: "mcp",
    scope: "household",
    group: "mcp",
    title: "MCP",
    description: "Client limits for every MCP server and every user.",
    icon: Plug,
    note: {
      text: "Register servers and approve their tools on the MCP Servers page.",
      linkLabel: "Open MCP Servers",
      to: "/mcp",
    },
    settings: [
      setting("mcp", "enabled", {
        title: "Enable MCP",
        description: "Kill switch. When off, no agent is offered an MCP tool and open MCP connections are closed.",
        keywords: ["model context protocol", "tools", "kill switch"],
        control: { kind: "switch" },
        defaultValue: true,
      }),
      setting("mcp", "connectTimeoutSeconds", {
        title: "Connect timeout",
        description: "Time allowed to connect to and initialize an MCP server.",
        keywords: ["model context protocol", "timeout"],
        control: { kind: "number", min: 1, max: 300, unit: "s" },
        defaultValue: 30,
      }),
      setting("mcp", "callTimeoutSeconds", {
        title: "Call timeout",
        description: "Time allowed for a single MCP tool call.",
        keywords: ["model context protocol", "timeout", "tool"],
        control: { kind: "number", min: 1, max: 600, unit: "s" },
        defaultValue: 60,
      }),
      setting("mcp", "idleTimeoutMinutes", {
        title: "Idle timeout",
        description: "An unused MCP connection is closed after this long, and a Sandbox-hosted server container is stopped with it.",
        keywords: ["model context protocol", "timeout", "sandbox", "container"],
        control: { kind: "number", min: 1, max: 1440, unit: "min" },
        defaultValue: 15,
      }),
      setting("mcp", "maxResultChars", {
        title: "Max result size",
        description: "Longer tool results are truncated before they enter the conversation.",
        keywords: ["model context protocol", "truncate", "characters", "context"],
        control: { kind: "number", min: 1000, max: 200000, unit: "chars" },
        defaultValue: 16000,
      }),
    ],
  },
  {
    id: "monitoring",
    scope: "household",
    group: "vigil",
    title: "Monitoring",
    description: "How Vigil probes services and infrastructure and when it opens an incident.",
    icon: Activity,
    settings: [
      setting("vigil", "pollIntervalSeconds", {
        title: "Poll interval",
        description: "How often every target is probed.",
        keywords: ["vigil", "health", "probe", "frequency"],
        control: { kind: "number", min: 5, max: 3600, unit: "s" },
        defaultValue: 15,
      }),
      setting("vigil", "probeTimeoutSeconds", {
        title: "Probe timeout",
        description: "Time allowed for a single probe. Can't exceed the poll interval.",
        keywords: ["vigil", "health", "timeout"],
        control: { kind: "number", min: 1, max: 60, unit: "s" },
        defaultValue: 5,
        validate: (value, valueOf) =>
          Number(value) > Number(valueOf("vigil.pollIntervalSeconds")) ? "Must not exceed the poll interval." : null,
      }),
      setting("vigil", "degradedLatencyMs", {
        title: "Degraded latency",
        description: "A reachable target slower than this is reported as Degraded.",
        keywords: ["vigil", "health", "latency", "slow"],
        control: { kind: "number", min: 1, max: 60000, unit: "ms" },
        defaultValue: 1000,
      }),
      setting("vigil", "consecutiveFailuresToOpenIncident", {
        title: "Failures before an incident",
        description:
          "Consecutive Degraded cycles before an incident opens. Unhealthy targets always open one immediately.",
        keywords: ["vigil", "health", "incident", "alert"],
        control: { kind: "number", min: 1, max: 100, unit: "cycles" },
        defaultValue: 3,
      }),
      setting("vigil", "sampleHistorySize", {
        title: "Sample history",
        description: "Recent samples kept per target for the sparklines.",
        keywords: ["vigil", "health", "sparkline", "history"],
        control: { kind: "number", min: 10, max: 200, unit: "samples" },
        defaultValue: 60,
      }),
    ],
  },
];
