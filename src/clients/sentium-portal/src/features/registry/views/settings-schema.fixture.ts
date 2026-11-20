import type { SettingControlSchema, SettingSchema, SettingsSectionSchema } from "../types/settings";

/** What `GET /registry/settings/schema` returns to a Sovereign, as built from the settings contracts. */

const setting = (
  key: string,
  field: string,
  title: string,
  control: Partial<SettingControlSchema> & Pick<SettingControlSchema, "kind">,
  defaultValue: unknown,
  extra: Partial<Pick<SettingSchema, "keywords" | "nullable" | "description">> = {},
): SettingSchema => ({
  id: `${key}.${field}`,
  field,
  title,
  description: extra.description ?? `${title}.`,
  keywords: extra.keywords ?? [],
  control: { required: true, ...control },
  nullable: extra.nullable ?? false,
  defaultValue,
});

const section = (
  id: string,
  key: string,
  scope: SettingsSectionSchema["scope"],
  title: string,
  settings: SettingSchema[],
): SettingsSectionSchema => ({ id, key, scope, title, description: `${title} settings.`, settings });

export const SETTINGS_SCHEMA: SettingsSectionSchema[] = [
  section("agents", "harness", "personal", "Agents", [
    setting("harness", "isBuiltInHarnessEnabled", "Built-in governance harness", { kind: "switch" }, true),
    setting("harness", "isPromptEnhancementEnabled", "Prompt enhancement", { kind: "switch" }, false),
    setting(
      "harness",
      "userHarnessPrompt",
      "Global prompt",
      { kind: "textarea", required: false, maxLength: 16000, rows: 8, placeholder: "Examples" },
      "",
    ),
  ]),
  section("calendar", "calendar-user", "personal", "Calendar", [
    setting("calendar-user", "timeZone", "Time zone", { kind: "timezone", required: false }, null, { nullable: true }),
    setting("calendar-user", "dayStart", "Day starts at", { kind: "time", required: false }, null, { nullable: true }),
  ]),
  section("companion", "companion", "personal", "Companion & voice", [
    setting("companion", "isCompanionEnabled", "Show the Companion", { kind: "switch" }, true),
    setting("companion", "isVoiceEnabled", "Voice", { kind: "switch" }, true),
    setting("companion", "voice", "Voice to speak with", { kind: "voice", required: false, maxLength: 64 }, null, {
      nullable: true,
    }),
    setting(
      "companion",
      "speakingRate",
      "Speaking rate",
      { kind: "range", min: 0.5, max: 2, step: 0.1, minLabel: "Slow (0.5×)", maxLabel: "Fast (2×)" },
      1,
    ),
    setting("companion", "isReviewBeforeSendingEnabled", "Review before sending", { kind: "switch" }, false),
    setting("companion", "isSpeakTypedRepliesEnabled", "Speak replies to typed messages", { kind: "switch" }, false),
  ]),
  section("evaluations", "evaluation", "personal", "Evaluations", [
    setting(
      "evaluation",
      "judgeModel",
      "Judge model",
      { kind: "model", required: false, maxLength: 256, emptyLabel: "Default model" },
      null,
      { nullable: true },
    ),
  ]),
  section("inference", "inference", "household", "Inference", [
    setting("inference", "defaultModel", "Default model", { kind: "model", maxLength: 256 }, "gemma-4-e4b"),
    setting(
      "inference",
      "agentTemperature",
      "Temperature",
      { kind: "range", min: 0, max: 1, step: 0.05, minLabel: "Precise (0.0)", maxLabel: "Creative (1.0)" },
      0.3,
    ),
  ]),
  section("household-calendar", "calendar", "household", "Household calendar", [
    setting("calendar", "timeZone", "Time zone", { kind: "timezone", required: false }, "UTC"),
    setting("calendar", "dayStart", "Day starts at", { kind: "time" }, "08:00:00"),
  ]),
  section("security", "pdp", "household", "Security", [
    setting("pdp", "semanticIntentCheckEnabled", "Semantic intent check", { kind: "switch" }, true),
    setting(
      "pdp",
      "intentCheckModel",
      "Intent check model",
      { kind: "model", required: false, maxLength: 256, emptyLabel: "Default model" },
      "",
    ),
    setting(
      "pdp",
      "autonomyLevel",
      "Autonomy level",
      { kind: "range", min: 1, max: 10, step: 1, minLabel: "High security", maxLabel: "High autonomy" },
      5,
    ),
    setting("pdp", "rateLimitMaxRequests", "Rate limit: requests", { kind: "number", min: 1, unit: "requests" }, 120),
    setting("pdp", "rateLimitWindowSeconds", "Rate limit: window", { kind: "number", min: 1, unit: "s" }, 60),
  ]),
  section("access", "access", "household", "Access", [
    setting("access", "registrationOpen", "Open registration", { kind: "switch" }, false),
  ]),
  section("mcp", "mcp", "household", "MCP", [
    setting("mcp", "enabled", "Enable MCP", { kind: "switch" }, true),
    setting("mcp", "connectTimeoutSeconds", "Connect timeout", { kind: "number", min: 1, max: 300, unit: "s" }, 30, {
      keywords: ["timeout"],
    }),
    setting("mcp", "callTimeoutSeconds", "Call timeout", { kind: "number", min: 1, max: 600, unit: "s" }, 60, {
      keywords: ["timeout"],
    }),
    setting("mcp", "idleTimeoutMinutes", "Idle timeout", { kind: "number", min: 1, max: 1440, unit: "min" }, 15, {
      keywords: ["timeout"],
    }),
    setting("mcp", "maxResultChars", "Max result size", { kind: "number", min: 1000, max: 200000, unit: "chars" }, 16000),
  ]),
  section("monitoring", "vigil", "household", "Monitoring", [
    setting("vigil", "pollIntervalSeconds", "Poll interval", { kind: "number", min: 5, max: 3600, unit: "s" }, 15),
    setting("vigil", "probeTimeoutSeconds", "Probe timeout", { kind: "number", min: 1, max: 60, unit: "s" }, 5, {
      keywords: ["timeout"],
    }),
    setting("vigil", "degradedLatencyMs", "Degraded latency", { kind: "number", min: 1, max: 60000, unit: "ms" }, 1000),
    setting(
      "vigil",
      "consecutiveFailuresToOpenIncident",
      "Failures before an incident",
      { kind: "number", min: 1, max: 100, unit: "cycles" },
      3,
    ),
    setting("vigil", "sampleHistorySize", "Sample history", { kind: "number", min: 10, max: 200, unit: "samples" }, 60),
  ]),
];
