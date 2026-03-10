import { NavLink } from "react-router";
import styles from "./navbar.module.scss";

const Navbar = () => {
  return (
    <nav className={styles.nav}>
      <div className={styles.navBrand}>
        <div className="status-dot"></div>
        <span>SENTIUM</span>
      </div>

      <div className={styles.navLinks}>
        <NavLink
          to="/"
          className={({ isActive }) =>
            isActive ? `${styles.navLink} ${styles.active}` : styles.navLink
          }
        >
          <span className={styles.cursorPrefix}>&gt; </span>HOME
        </NavLink>

        <NavLink
          to="/terminal"
          className={({ isActive }) =>
            isActive ? `${styles.navLink} ${styles.active}` : styles.navLink
          }
        >
          <span className={styles.cursorPrefix}>&gt; </span>ORCHESTRATOR
        </NavLink>

        <NavLink
          to="/agents"
          className={({ isActive }) =>
            isActive ? `${styles.navLink} ${styles.active}` : styles.navLink
          }
        >
          <span className={styles.cursorPrefix}>&gt; </span>AGENTS
        </NavLink>
      </div>
    </nav>
  );
};

export default Navbar;
