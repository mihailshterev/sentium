import { Outlet } from "react-router";
import Navbar from "./navbar";
import styles from "./layout.module.scss";

const Layout = () => {
  return (
    <div className={styles.layoutWrapper}>
      <Navbar />
      <main className={styles.contentArea}>
        <Outlet />
      </main>
    </div>
  );
};

export default Layout;
