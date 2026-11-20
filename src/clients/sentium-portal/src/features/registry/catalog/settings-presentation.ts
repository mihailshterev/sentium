import { Activity, AudioLines, Bot, CalendarClock, Cpu, FlaskConical, Home, Plug, ShieldCheck, UserPlus } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { SettingDefinition, SettingScope, SettingsSection } from "../types/settings";

/**
 * What the server's settings schema can't carry: icons, route links, formatter functions and e2e hooks. Everything
 * else (titles, bounds, defaults, controls) comes from the settings contracts, so a new setting needs no entry here.
 */
export interface SectionPresentation {
  icon: LucideIcon;
  note?: SettingsSection["note"];
}

export interface SettingPresentation {
  /** Replaces the default `value.toFixed(<step decimals>)` of a range. */
  format?: (value: number) => string;
  /** Extra line under a range that explains the selected value. */
  explain?: (value: number) => string;
  testId?: string;
  /** A rule that spans settings; the server enforces it too. */
  validate?: SettingDefinition["validate"];
}

export const SCOPES: { id: SettingScope; title: string; description: string }[] = [
  { id: "personal", title: "Personal", description: "Only affects you" },
  { id: "household", title: "Household", description: "Applies to everyone · Sovereign only" },
];

const autonomyName = (level: number) =>
  level <= 2 ? "Max security" : level >= 9 ? "Max autonomy" : `Level ${level}`;

const autonomyEffect = (level: number) =>
  level <= 2
    ? "Inconclusive intent checks are treated as denials."
    : level >= 9
      ? "The semantic intent check is bypassed entirely."
      : "Semantic checks run normally; an inconclusive check allows the action.";

/** Keyed by section id. */
export const SECTION_PRESENTATION: Record<string, SectionPresentation> = {
  agents: { icon: Bot },
  calendar: { icon: CalendarClock },
  companion: { icon: AudioLines },
  evaluations: { icon: FlaskConical },
  inference: { icon: Cpu },
  "household-calendar": { icon: Home },
  security: {
    icon: ShieldCheck,
    note: {
      text: "Lockdown mode is an emergency switch and lives on the Sentinel page.",
      linkLabel: "Open Sentinel",
      to: "/sentinel",
    },
  },
  access: { icon: UserPlus },
  mcp: {
    icon: Plug,
    note: {
      text: "Register servers and approve their tools on the MCP Servers page.",
      linkLabel: "Open MCP Servers",
      to: "/mcp",
    },
  },
  monitoring: { icon: Activity },
};

/** Keyed by setting id (`{key}.{field}`). */
export const SETTING_PRESENTATION: Record<string, SettingPresentation> = {
  "companion.isCompanionEnabled": { testId: "companion-enabled-toggle" },
  "companion.speakingRate": { format: (v) => `${v.toFixed(1)}×` },
  "pdp.semanticIntentCheckEnabled": { testId: "semantic-intent-toggle" },
  "pdp.autonomyLevel": { format: autonomyName, explain: autonomyEffect, testId: "autonomy-slider" },
  "vigil.probeTimeoutSeconds": {
    validate: (value, valueOf) =>
      Number(value) > Number(valueOf("vigil.pollIntervalSeconds")) ? "Must not exceed the poll interval." : null,
  },
};
