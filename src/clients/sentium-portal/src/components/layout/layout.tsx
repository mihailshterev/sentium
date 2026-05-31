import { Outlet } from "react-router";
import Navbar from "../navbar";
import AuroraBackground from "../ui/aurora-background";
import styles from "./layout.module.scss";

const Layout = () => {
  return (
    <div className={styles.layoutWrapper}>
      <AuroraBackground />
      <Navbar />
      <main className={styles.contentArea}>
        <Outlet />
      </main>
    </div>
  );
};

export default Layout;
