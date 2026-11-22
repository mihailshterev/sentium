import { Search } from "lucide-react";
import { COMMAND_PALETTE_SHORTCUT, openCommandPalette } from "../command-palette/open-command-palette";
import { AccountMenu } from "./account-menu";
import { Breadcrumbs } from "./breadcrumbs";
import { TopbarClock } from "./topbar-clock";
import { NotificationBell } from "@/features/notifications";
import styles from "./topbar.module.css";

/** Global bar above every page: where you are, search, notifications and the account menu. */
export function Topbar() {
  return (
    <header className={styles.topbar}>
      <Breadcrumbs />
      <div className={styles.actions}>
        <TopbarClock />
        <button
          type="button"
          className={styles.search}
          onClick={openCommandPalette}
          aria-keyshortcuts="Control+K Meta+K"
          aria-label="Search or jump to"
        >
          <Search size={15} aria-hidden="true" />
          <span className={styles.searchText}>Search or jump to…</span>
          <kbd className={styles.kbd}>{COMMAND_PALETTE_SHORTCUT}</kbd>
        </button>
        <NotificationBell />
        <AccountMenu />
      </div>
    </header>
  );
}
