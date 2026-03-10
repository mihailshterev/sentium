import { Outlet } from "react-router";
import Navbar from "./navbar";
import styles from "./layout.module.scss";

const Layout = () => {
  return (
    <div className={styles["sentium-layout-wrapper"]}>
      <Navbar />
      <main className={styles["content-area"]}>
        <Outlet />
      </main>
    </div>
  );
};

export default Layout;
