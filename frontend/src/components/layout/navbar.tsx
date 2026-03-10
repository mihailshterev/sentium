import { NavLink } from "react-router";
import styles from "./navbar.module.scss";

const Navbar = () => {
  return (
    <nav className={styles["sentium-nav"]}>
      <div className={styles["nav-brand"]}>
        <div className="status-dot"></div>
        <span>SENTIUM</span>
      </div>

      <div className={styles["nav-links"]}>
        <NavLink
          to="/"
          className={({ isActive }) =>
            isActive
              ? `${styles["nav-link"]} ${styles["active"]}`
              : styles["nav-link"]
          }
        >
          <span className={styles["cursor-prefix"]}>&gt; </span>HOME
        </NavLink>

        <NavLink
          to="/terminal"
          className={({ isActive }) =>
            isActive
              ? `${styles["nav-link"]} ${styles["active"]}`
              : styles["nav-link"]
          }
        >
          <span className={styles["cursor-prefix"]}>&gt; </span>ORCHESTRATOR
        </NavLink>

        <NavLink
          to="/agents"
          className={({ isActive }) =>
            isActive
              ? `${styles["nav-link"]} ${styles["active"]}`
              : styles["nav-link"]
          }
        >
          <span className={styles["cursor-prefix"]}>&gt; </span>AGENTS
        </NavLink>
      </div>
    </nav>
  );
};

export default Navbar;
