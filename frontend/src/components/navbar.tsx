import { NavLink } from "react-router";
import {
  Activity,
  Bot,
  BotMessageSquare,
  BrickWallShield,
  Cpu,
  GitBranch,
  LayoutDashboard,
  Package,
  Settings,
  UsersRound,
  View,
} from "lucide-react";
import styles from "./navbar.module.scss";
import React from "react";

const NAV_LINKS = [
  // Group: Main
  {
    to: "/",
    label: "Dashboard",
    icon: LayoutDashboard,
    end: true,
    group: "main",
  },
  {
    to: "/sentinel",
    label: "Sentinel",
    icon: BrickWallShield,
    group: "main",
  },
  { to: "/watchdog", label: "Watchdog", icon: View, group: "main" },

  // Group: AI
  {
    to: "/assistant",
    label: "Assistant",
    icon: BotMessageSquare,
    group: "ai",
  },
  {
    to: "/orchestration",
    label: "Orchestration",
    icon: Activity,
    group: "ai",
  },
  { to: "/agents", label: "Agents", icon: Bot, group: "ai" },
  { to: "/workflows", label: "Workflows", icon: GitBranch, group: "ai" },

  // Group: Management
  { to: "/users", label: "Users", icon: UsersRound, group: "management" },
  {
    to: "/inventory",
    label: "Assets & Inventory",
    icon: Package,
    group: "management",
  },
];

const Navbar = () => {
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
          {NAV_LINKS.map(({ to, label, icon: Icon, end, group }, index) => {
            const isNewGroup =
              index > 0 && group !== NAV_LINKS[index - 1].group;

            return (
              <React.Fragment key={to}>
                {isNewGroup && <div className={styles.navDivider} />}
                <NavLink to={to} end={end} className={getLinkClass}>
                  <Icon size={15} className={styles.navIcon} />
                  <span>{label}</span>
                </NavLink>
              </React.Fragment>
            );
          })}
          <div className={styles.navSectionBottom}>
            <NavLink to="/system" className={getLinkClass}>
              <Cpu size={18} className={styles.navIcon} />
              <span>System</span>
            </NavLink>

            <NavLink to="/settings" className={getLinkClass}>
              <Settings size={18} className={styles.navIcon} />
              <span>Settings</span>
            </NavLink>
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
