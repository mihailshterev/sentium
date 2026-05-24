import { NavLink } from "react-router";
import {
  Bot,
  BotMessageSquare,
  BrainCircuit,
  Cpu,
  Database,
  FolderOpen,
  GitBranch,
  LayoutDashboard,
  LogOut,
  Waypoints,
  Orbit,
  Settings,
  ShieldCog,
  ShieldUser,
  UsersRound,
  VectorSquare,
  View,
  Zap,
  type LucideIcon,
  CalendarClock,
} from "lucide-react";
import styles from "./navbar.module.scss";
import React from "react";
import { useAuthStore } from "../stores/auth-store";
import { useRole } from "../hooks/useRole";

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
    links: [{ to: "/", label: "Dashboard", icon: LayoutDashboard, end: true }],
  },
  {
    id: "ai",
    label: "AI & Automation",
    links: [
      { to: "/assistant", label: "Assistant", icon: BotMessageSquare },
      { to: "/orchestration", label: "Orchestration", icon: Orbit },
      { to: "/workflows", label: "Workflows", icon: GitBranch },
      { to: "/agents", label: "Agents", icon: Bot },
      { to: "/models", label: "Models", icon: BrainCircuit },
      { to: "/skills", label: "Skills", icon: Zap },
    ],
  },
  {
    id: "management",
    label: "Management",
    links: [
      { to: "/workspaces", label: "Workspaces", icon: FolderOpen },
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
      { to: "/watchdog", label: "Watchdog", icon: View },
    ],
  },
];

const Navbar = () => {
  const logout = useAuthStore((s) => s.logout);
  const user = useAuthStore((s) => s.user);
  const { isSovereign, highestRole } = useRole();

  const getLinkClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? `${styles.navLink} ${styles.active}` : styles.navLink;

  const displayName = user?.name && user.name.trim() !== user.email ? user.name : (user?.email ?? "");

  return (
    <nav className={styles.nav}>
      <div className={styles.navBrand}>
        <div className={styles.brandIcon}>
          <Cpu size={16} />
        </div>
        <div className={styles.brandText}>
          <span className={styles.brandName}>SENTIUM</span>
          <span className={styles.brandStatus}>
            <span className="status-dot"></span>
            ONLINE
          </span>
        </div>
      </div>

      <NavLink
        to="/profile"
        className={({ isActive }) => (isActive ? `${styles.userCard} ${styles.userCardActive}` : styles.userCard)}
      >
        <div className={styles.userAvatar}>
          <ShieldUser size={20} />
        </div>
        <div className={styles.userInfo}>
          <span className={styles.userDisplayName}>{displayName}</span>
          {highestRole && (
            <span className={`${styles.userRoleBadge} ${styles[`roleColor_${highestRole.toLowerCase()}`]}`}>
              {highestRole}
            </span>
          )}
        </div>
      </NavLink>

      <div className={styles.navSection}>
        <div className={styles.navLinks}>
          <div className={styles.navDivider} />
          {NAV_GROUPS.map((group, groupIdx) => {
            const visibleLinks = group.links.filter((link) => !link.sovereignOnly || isSovereign);
            if (visibleLinks.length === 0) {
              return null;
            }

            return (
              <React.Fragment key={group.id}>
                {groupIdx > 0 && (
                  <>
                    <div className={styles.navDivider} />
                    {group.label && <span className={styles.groupLabel}>{group.label}</span>}
                  </>
                )}
                {visibleLinks.map(({ to, label, icon: Icon, end }) => (
                  <NavLink key={to} to={to} end={end} className={getLinkClass}>
                    <Icon size={15} className={styles.navIcon} />
                    <span>{label}</span>
                  </NavLink>
                ))}
              </React.Fragment>
            );
          })}

          <div className={styles.navSectionBottom}>
            <div className={styles.navDivider} />
            <NavLink to="/system" className={getLinkClass}>
              <Cpu size={18} className={styles.navIcon} />
              <span>System</span>
            </NavLink>

            <NavLink to="/settings" className={getLinkClass}>
              <Settings size={18} className={styles.navIcon} />
              <span>Settings</span>
            </NavLink>

            <button className={styles.logoutBtn} onClick={logout}>
              <LogOut size={15} className={styles.navIcon} />
              <span>Log out</span>
            </button>
          </div>
        </div>
      </div>

      <div className={styles.navFooter}>
        <div className={styles.buildInfo}>
          <span className={styles.buildDot}></span>
          <span>v0.1.0-alpha</span>
        </div>
      </div>
    </nav>
  );
};

export default Navbar;
