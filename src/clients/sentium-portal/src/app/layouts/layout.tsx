import { Outlet, useLocation } from "react-router";
import { Navbar } from "../components/navbar/navbar";
import { CosmosBackground } from "@/shared/ui/cosmos-background";
import styles from "./layout.module.css";

export function Layout() {
  // The semantic map paints its own opaque canvas over everything, so there is nothing to animate behind it.
  const { pathname } = useLocation();
  const backgroundPaused = pathname.startsWith("/semantic-map");

  return (
    <div className={styles.layoutWrapper}>
      <CosmosBackground paused={backgroundPaused} />
      <Navbar />
      <main className={styles.contentArea}>
        <Outlet />
      </main>
    </div>
  );
}
