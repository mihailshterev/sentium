import { NavLink } from "react-router";
import {
  Bot,
  BotMessageSquare,
  BrickWallShield,
  Cpu,
  FolderOpen,
  GitBranch,
  LayoutDashboard,
  LogOut,
  Orbit,
  Package,
  Settings,
  UsersRound,
  View,
  type LucideIcon,
} from "lucide-react";
import styles from "./navbar.module.scss";
import React from "react";
import { useAuthStore } from "../stores/auth-store";

interface NavLinkItem {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
}

interface NavGroup {
  id: string;
  links: NavLinkItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    id: "main",
    links: [{ to: "/", label: "Dashboard", icon: LayoutDashboard, end: true }],
  },
  {
    id: "ai",
    links: [
      { to: "/assistant", label: "Assistant", icon: BotMessageSquare },
      { to: "/orchestration", label: "Orchestration", icon: Orbit },
      { to: "/workflows", label: "Workflows", icon: GitBranch },
      { to: "/agents", label: "Agents", icon: Bot },
    ],
  },
  {
    id: "management",
    links: [
      { to: "/workspaces", label: "Workspaces", icon: FolderOpen },
      { to: "/inventory", label: "Assets & Inventory", icon: Package },
      { to: "/users", label: "Users", icon: UsersRound },
    ],
  },
  {
    id: "security",
    links: [
      { to: "/sentinel", label: "Sentinel", icon: BrickWallShield },
      { to: "/watchdog", label: "Watchdog", icon: View },
    ],
  },
];

const Navbar = () => {
  const logout = useAuthStore((s) => s.logout);

  const getLinkClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? `${styles.navLink} ${styles.active}` : styles.navLink;

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

      <div className={styles.navSection}>
        <div className={styles.navLinks}>
          {NAV_GROUPS.map((group, groupIdx) => (
            <React.Fragment key={group.id}>
              {groupIdx > 0 && <div className={styles.navDivider} />}
              {group.links.map(({ to, label, icon: Icon, end }) => (
                <NavLink key={to} to={to} end={end} className={getLinkClass}>
                  <Icon size={15} className={styles.navIcon} />
                  <span>{label}</span>
                </NavLink>
              ))}
            </React.Fragment>
          ))}
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
