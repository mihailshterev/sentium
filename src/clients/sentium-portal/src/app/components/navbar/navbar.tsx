import { useEffect } from "react";
import { NavLink } from "react-router";
import { MotionConfig, motion } from "framer-motion";
import {
  BookOpen,
  Bot,
  BotMessageSquare,
  BrainCircuit,
  CalendarClock,
  CalendarRange,
  Cpu,
  Database,
  FolderOpen,
  GitBranch,
  LayoutDashboard,
  LogOut,
  Orbit,
  Package,
  PanelLeftClose,
  PanelLeftOpen,
  Settings,
  ShieldCog,
  UsersRound,
  VectorSquare,
  View,
  Waypoints,
  type LucideIcon,
} from "lucide-react";
import styles from "./navbar.module.css";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import { useOrchestrationRunStore } from "@/features/orchestration/state/orchestration-run-store";
import { useTimelineChatStore } from "@/features/timeline/state/timeline-chat-store";
import { useRole } from "@/shared/auth/hooks/useRole";
import { useProfile } from "@/features/profile/hooks/useProfile";
import { useMediaQuery } from "@/shared/hooks/useMediaQuery";
import { useUiStore } from "@/shared/ui/state/ui-store";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ThemeToggle } from "@/shared/ui/theme-toggle";
import { Tooltip } from "@/shared/ui/tooltip";

interface NavLinkItem {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
  sovereignOnly?: boolean;
}

interface NavGroup {
  id: string;
  label?: string;
  links: NavLinkItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    id: "main",
    links: [
      { to: "/", label: "Dashboard", icon: LayoutDashboard, end: true },
      { to: "/timeline", label: "Timeline", icon: CalendarRange },
    ],
  },
  {
    id: "ai",
    label: "AI & automation",
    links: [
      { to: "/assistant", label: "Assistant", icon: BotMessageSquare },
      { to: "/orchestration", label: "Orchestration", icon: Orbit },
      { to: "/workflows", label: "Workflows", icon: GitBranch },
      { to: "/agents", label: "Agents", icon: Bot },
      { to: "/models", label: "Models", icon: BrainCircuit },
      { to: "/skills", label: "Skills", icon: BookOpen },
    ],
  },
  {
    id: "management",
    label: "Management",
    links: [
      { to: "/workspaces", label: "Workspaces", icon: FolderOpen },
      { to: "/inventory", label: "Inventory", icon: Package },
      { to: "/knowledge-base", label: "Knowledge Base", icon: Database },
      { to: "/semantic-map", label: "Semantic Map", icon: Waypoints },
      { to: "/scheduler", label: "Scheduled Jobs", icon: CalendarClock },
      { to: "/users", label: "Users", icon: UsersRound, sovereignOnly: true },
    ],
  },
  {
    id: "security",
    label: "Security",
    links: [
      { to: "/sentinel", label: "Sentinel", icon: ShieldCog },
      { to: "/sandbox", label: "Sandbox", icon: VectorSquare },
      { to: "/vigil", label: "Vigil", icon: View },
    ],
  },
  {
    id: "platform",
    label: "Platform",
    links: [
      { to: "/system", label: "System", icon: Cpu },
      { to: "/settings", label: "Settings", icon: Settings },
    ],
  },
];

const ACTIVE_PILL_TRANSITION = { type: "spring", stiffness: 520, damping: 40 } as const;

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return (parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : (parts[0] ?? "?").slice(0, 2)).toUpperCase();
}

function isEditable(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  return !!el && (el.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(el.tagName));
}

interface NavItemProps extends NavLinkItem {
  collapsed: boolean;
  busy: boolean;
}

function NavItem({ to, label, icon: Icon, end, collapsed, busy }: NavItemProps) {
  return (
    <Tooltip content={label} side="right" disabled={!collapsed}>
      <NavLink
        to={to}
        end={end}
        className={({ isActive }) => (isActive ? `${styles.link} ${styles.active}` : styles.link)}
      >
        {({ isActive }) => (
          <>
            {isActive && (
              <motion.span
                layoutId="nav-active-pill"
                className={styles.activePill}
                transition={ACTIVE_PILL_TRANSITION}
              />
            )}
            <Icon size={18} strokeWidth={1.75} className={styles.icon} aria-hidden="true" />
            <span className={styles.label}>{label}</span>
            {busy && (
              <span className={styles.activity}>
                <span className="visually-hidden">running</span>
              </span>
            )}
          </>
        )}
      </NavLink>
    </Tooltip>
  );
}

export function Navbar() {
  const logout = useAuthStore((s) => s.logout);
  const user = useAuthStore((s) => s.user);
  const { isSovereign, highestRole } = useRole();
  const { profile } = useProfile();

  const assistantStreaming = useConversationStore((s) => s.isStreaming);
  const orchestrationRunning = useOrchestrationRunStore((s) => s.isRunning);
  const timelineChatStreaming = useTimelineChatStore((s) => s.isStreaming);

  const storedCollapsed = useUiStore((s) => s.sidebarCollapsed);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const isNarrow = useMediaQuery("(max-width: 1099px)");
  const collapsed = isNarrow || storedCollapsed;

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === "b" && !isEditable(e.target)) {
        e.preventDefault();
        toggleSidebar();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [toggleSidebar]);

  const busyRoutes = new Set<string>();
  if (assistantStreaming) {
    busyRoutes.add("/assistant");
  }

  if (orchestrationRunning) {
    busyRoutes.add("/orchestration");
  }

  if (timelineChatStreaming) {
    busyRoutes.add("/timeline");
  }

  const displayName = profile
    ? [profile.firstName, profile.lastName].filter(Boolean).join(" ") || profile.userName
    : (user?.name ?? "");

  const toggleLabel = collapsed ? "Expand sidebar" : "Collapse sidebar";
  const actionTooltipSide = collapsed ? "right" : "top";

  return (
    <MotionConfig reducedMotion="user">
      <nav className={`frame ${styles.nav}`} data-collapsed={collapsed || undefined} aria-label="Main navigation">
        <div className={styles.brand}>
          <img src="/sentium-mark.png" alt="" className={styles.logo} width={28} height={28} />
          <span className={styles.wordmark}>Sentium</span>
        </div>

        <div className={styles.scroll}>
          {NAV_GROUPS.map((group) => {
            const links = group.links.filter((link) => !link.sovereignOnly || isSovereign);
            if (links.length === 0) {
              return null;
            }
            return (
              <div key={group.id} className={styles.group}>
                {group.label && (
                  <span className={styles.groupLabel} aria-hidden={collapsed || undefined}>
                    {group.label}
                  </span>
                )}
                {links.map((link) => (
                  <NavItem key={link.to} {...link} collapsed={collapsed} busy={busyRoutes.has(link.to)} />
                ))}
              </div>
            );
          })}
        </div>

        <div className={styles.footer}>
          <Tooltip content={displayName || "Profile"} side="right" disabled={!collapsed}>
            <NavLink
              to="/profile"
              className={({ isActive }) => (isActive ? `${styles.account} ${styles.accountActive}` : styles.account)}
            >
              <span className={styles.avatar} aria-hidden="true">
                {initialsOf(displayName)}
              </span>
              <span className={styles.userInfo}>
                <span className={styles.userDisplayName}>{displayName}</span>
                {highestRole && (
                  <Badge tone={highestRole === "Sovereign" ? "accent" : "info"} className={styles.roleBadge}>
                    {highestRole}
                  </Badge>
                )}
              </span>
            </NavLink>
          </Tooltip>

          <div className={styles.actions}>
            <Tooltip content="Toggle theme" side={actionTooltipSide}>
              <ThemeToggle />
            </Tooltip>
            {!isNarrow && (
              <Tooltip content={`${toggleLabel} (Ctrl+B)`} side={actionTooltipSide}>
                <Button
                  variant="ghost"
                  size="sm"
                  iconOnly
                  aria-label={toggleLabel}
                  aria-keyshortcuts="Control+B Meta+B"
                  onClick={toggleSidebar}
                  icon={collapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
                />
              </Tooltip>
            )}
            <Tooltip content="Log out" side={actionTooltipSide}>
              <Button
                variant="ghost"
                size="sm"
                iconOnly
                aria-label="Log out"
                onClick={logout}
                icon={<LogOut size={16} />}
                className={styles.logout}
              />
            </Tooltip>
            <span className={styles.version}>v1.0.0</span>
          </div>
        </div>
      </nav>
    </MotionConfig>
  );
}
