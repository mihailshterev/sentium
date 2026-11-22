import { useEffect, useId, useRef, useState } from "react";
import { Link } from "react-router";
import { ChevronDown, LogOut, Moon, Sun } from "lucide-react";
import { ACCOUNT_LINKS, visibleLinks } from "../navbar/nav-config";
import { useProfile } from "@/features/profile/hooks/useProfile";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { useRole } from "@/shared/auth/hooks/useRole";
import { resolveTheme, useThemeStore } from "@/shared/ui/state/theme-store";
import { Avatar } from "@/shared/ui/avatar";
import { Badge } from "@/shared/ui/badge";
import styles from "./topbar.module.css";

function useDisplayName(): { name: string; avatarUrl?: string | null } {
  const user = useAuthStore((s) => s.user);
  const { profile } = useProfile();
  const name = profile
    ? [profile.firstName, profile.lastName].filter(Boolean).join(" ") || profile.userName
    : (user?.name ?? "");
  return { name, avatarUrl: profile?.avatarUrl };
}

const menuItems = (menu: HTMLElement | null) => [...(menu?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])];

/** Avatar button opening the account menu: account pages, theme and sign-out (kept away from everyday controls). */
export function AccountMenu() {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const menuId = useId();

  const logout = useAuthStore((s) => s.logout);
  const { isSovereign, highestRole } = useRole();
  const { name, avatarUrl } = useDisplayName();
  const isDark = resolveTheme(useThemeStore((s) => s.preference)) === "dark";
  const toggleTheme = useThemeStore((s) => s.toggle);

  useEffect(() => {
    if (!open) {
      return;
    }
    menuItems(menuRef.current)[0]?.focus();

    const onPointer = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("pointerdown", onPointer);
    return () => document.removeEventListener("pointerdown", onPointer);
  }, [open]);

  const close = (restoreFocus: boolean) => {
    setOpen(false);
    if (restoreFocus) {
      triggerRef.current?.focus();
    }
  };

  const onMenuKeyDown = (e: React.KeyboardEvent) => {
    const items = menuItems(menuRef.current);
    const index = items.indexOf(document.activeElement as HTMLElement);
    const focusAt = (i: number) => items[(i + items.length) % items.length]?.focus();

    switch (e.key) {
      case "Escape":
        e.preventDefault();
        close(true);
        break;
      case "Tab":
        close(false);
        break;
      case "ArrowDown":
        e.preventDefault();
        focusAt(index + 1);
        break;
      case "ArrowUp":
        e.preventDefault();
        focusAt(index - 1);
        break;
      case "Home":
        e.preventDefault();
        focusAt(0);
        break;
      case "End":
        e.preventDefault();
        focusAt(-1);
        break;
    }
  };

  return (
    <div ref={rootRef} className={styles.menuRoot}>
      <button
        ref={triggerRef}
        type="button"
        className={styles.accountTrigger}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={`Account menu, ${name}`}
        onClick={() => setOpen((o) => !o)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" && !open) {
            e.preventDefault();
            setOpen(true);
          }
        }}
      >
        <Avatar name={name} src={avatarUrl} size="sm" />
        <span className={styles.accountName}>{name}</span>
        <ChevronDown size={14} className={styles.accountChevron} aria-hidden="true" />
      </button>

      {open && (
        <div
          ref={menuRef}
          id={menuId}
          role="menu"
          aria-label="Account"
          className={`frame ${styles.menu}`}
          onKeyDown={onMenuKeyDown}
        >
          <div className={styles.menuHeader}>
            <Avatar name={name} src={avatarUrl} size="md" />
            <div className={styles.menuIdentity}>
              <span className={styles.menuName}>{name}</span>
              {highestRole && <Badge tone={highestRole === "Sovereign" ? "accent" : "info"}>{highestRole}</Badge>}
            </div>
          </div>

          {visibleLinks(ACCOUNT_LINKS, isSovereign).map((link) => (
            <Link
              key={link.to}
              to={link.to}
              role="menuitem"
              tabIndex={-1}
              className={styles.menuItem}
              onClick={() => close(false)}
            >
              <link.icon size={15} aria-hidden="true" />
              {link.label}
            </Link>
          ))}

          <button type="button" role="menuitem" tabIndex={-1} className={styles.menuItem} onClick={toggleTheme}>
            {isDark ? <Sun size={15} aria-hidden="true" /> : <Moon size={15} aria-hidden="true" />}
            {isDark ? "Switch to light theme" : "Switch to dark theme"}
          </button>

          <hr className={styles.menuDivider} />

          <button
            type="button"
            role="menuitem"
            tabIndex={-1}
            className={`${styles.menuItem} ${styles.menuItemDanger}`}
            onClick={() => {
              close(false);
              void logout();
            }}
          >
            <LogOut size={15} aria-hidden="true" />
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}
