import { Suspense } from "react";
import { Loader2 } from "lucide-react";
import { Outlet, useLocation } from "react-router";
import { Navbar } from "../components/navbar/navbar";
import { Companion, PHONE_QUERY, useCompanionRuntime } from "@/features/companion";
import { useMediaQuery } from "@/shared/hooks/useMediaQuery";
import { CosmosBackground } from "@/shared/ui/cosmos-background";
import styles from "./layout.module.css";

export function Layout() {
  // The semantic map paints its own opaque canvas over everything, so there is nothing to animate behind it.
  const { pathname } = useLocation();
  const backgroundPaused = pathname.startsWith("/semantic-map");

  // Mounted here, above the routes, so the Companion's state and speech survive navigation.
  useCompanionRuntime();
  const isPhone = useMediaQuery(PHONE_QUERY);

  return (
    <div className={styles.layoutWrapper}>
      <CosmosBackground paused={backgroundPaused} />
      <Navbar />
      <main className={styles.contentArea}>
        <Suspense
          fallback={
            <div className={styles.routeFallback} role="status" aria-label="Loading page">
              <Loader2 className="animate-spin" size={22} aria-hidden="true" />
            </div>
          }
        >
          <Outlet />
        </Suspense>
      </main>
      {isPhone && <Companion placement="floating" />}
    </div>
  );
}
