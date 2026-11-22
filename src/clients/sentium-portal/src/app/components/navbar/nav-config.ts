import {
  BookOpen,
  Bot,
  BotMessageSquare,
  BrainCircuit,
  CalendarClock,
  CalendarRange,
  Cpu,
  Database,
  FlaskConical,
  FolderOpen,
  GitBranch,
  Home,
  Inbox,
  Library,
  Orbit,
  Package,
  Palette,
  Plug,
  Settings,
  ShieldCog,
  UserRound,
  UsersRound,
  VectorSquare,
  View,
  Waypoints,
  type LucideIcon,
} from "lucide-react";

export interface NavLinkItem {
  to: string;
  label: string;
  /** What the page is for; shown in the command palette and matched by its search. */
  description: string;
  icon: LucideIcon;
  end?: boolean;
  sovereignOnly?: boolean;
}

export interface NavGroup {
  id: string;
  label: string;
  links: NavLinkItem[];
}

export const HOME_LINK: NavLinkItem = {
  to: "/",
  label: "Home",
  description: "What needs attention and where you left off",
  icon: Home,
  end: true,
};

/** The sidebar, grouped by what the user is doing rather than by backend module. */
export const NAV_GROUPS: NavGroup[] = [
  {
    id: "work",
    label: "Work",
    links: [
      { to: "/assistant", label: "Assistant", description: "Chat with an agent", icon: BotMessageSquare },
      { to: "/orchestration", label: "Orchestration", description: "Hand a goal to a squad of agents", icon: Orbit },
      { to: "/timeline", label: "Timeline", description: "Your year plan and milestones", icon: CalendarRange },
      { to: "/workspaces", label: "Workspaces", description: "Files and context for a task", icon: FolderOpen },
    ],
  },
  {
    id: "knowledge",
    label: "Knowledge",
    links: [
      {
        to: "/knowledge-base",
        label: "Knowledge Base",
        description: "Document collections the agents search",
        icon: Database,
      },
      { to: "/vault", label: "Vault", description: "Your synced Obsidian notes", icon: Library },
      { to: "/semantic-map", label: "Semantic Map", description: "Explore the embeddings visually", icon: Waypoints },
      { to: "/inventory", label: "Inventory", description: "Household assets and their documents", icon: Package },
    ],
  },
  {
    id: "build",
    label: "Build",
    links: [
      { to: "/agents", label: "Agents", description: "Agents, their prompts and tools", icon: Bot },
      { to: "/workflows", label: "Workflows", description: "Fixed multi-agent pipelines", icon: GitBranch },
      { to: "/skills", label: "Skills", description: "Reusable agent instructions", icon: BookOpen },
      { to: "/models", label: "Models", description: "Local language and embedding models", icon: BrainCircuit },
      { to: "/evaluations", label: "Evaluations", description: "Eval suites and benchmarks", icon: FlaskConical },
      {
        to: "/mcp",
        label: "MCP Servers",
        description: "External tool servers",
        icon: Plug,
        sovereignOnly: true,
      },
    ],
  },
  {
    id: "operate",
    label: "Operate",
    links: [
      { to: "/scheduler", label: "Scheduled Jobs", description: "Cron jobs and agent schedules", icon: CalendarClock },
      { to: "/sandbox", label: "Sandbox", description: "Isolated script runs", icon: VectorSquare },
      { to: "/sentinel", label: "Sentinel", description: "Policy decisions and the tool-call audit", icon: ShieldCog },
      { to: "/vigil", label: "Vigil", description: "Service health, incidents and monitoring", icon: View },
      { to: "/system", label: "System", description: "Host CPU, memory, disks and runtime", icon: Cpu },
    ],
  },
];

/** Account and administration pages: reached from the account menu, not the sidebar. */
export const ACCOUNT_LINKS: NavLinkItem[] = [
  { to: "/profile", label: "Profile", description: "Your account and avatar", icon: UserRound },
  { to: "/inbox", label: "Inbox", description: "Your notifications", icon: Inbox },
  { to: "/settings", label: "Settings", description: "Personal and household settings", icon: Settings },
  {
    to: "/users",
    label: "Users",
    description: "Household members and roles",
    icon: UsersRound,
    sovereignOnly: true,
  },
];

export const QUICK_ACTIONS: NavLinkItem[] = [
  { to: "/assistant", label: "New conversation", description: "Start a chat with an agent", icon: BotMessageSquare },
  { to: "/orchestration", label: "Orchestrate a goal", description: "Plan, run and check with agents", icon: Orbit },
  { to: "/workflows/new", label: "New workflow", description: "Open the workflow builder", icon: GitBranch },
  { to: "/settings/appearance", label: "Change appearance", description: "Theme and colour palette", icon: Palette },
];

export function visibleLinks(links: NavLinkItem[], isSovereign: boolean): NavLinkItem[] {
  return links.filter((link) => !link.sovereignOnly || isSovereign);
}

const matchesPath = (link: NavLinkItem, pathname: string) =>
  pathname === link.to || (link.to !== "/" && pathname.startsWith(`${link.to}/`));

export function findNavGroup(pathname: string): NavGroup | undefined {
  return NAV_GROUPS.find((group) => group.links.some((link) => matchesPath(link, pathname)));
}

export function findNavLink(pathname: string): NavLinkItem | undefined {
  return [...NAV_GROUPS.flatMap((group) => group.links), ...ACCOUNT_LINKS].find((link) => matchesPath(link, pathname));
}
