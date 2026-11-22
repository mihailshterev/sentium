import { useEffect, useId } from "react";
import { NavLink, useLocation } from "react-router";
import { ChevronDown, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { SharedLayoutIndicator } from "@/shared/ui/motion";
import styles from "./navbar.module.css";
import { HOME_LINK, NAV_GROUPS, findNavGroup, visibleLinks, type NavGroup, type NavLinkItem } from "./nav-config";
import { useConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import { useOrchestrationRunStore } from "@/features/orchestration/state/orchestration-run-store";
import { useTimelineChatStore } from "@/features/timeline/state/timeline-chat-store";
import { Companion, PHONE_QUERY } from "@/features/companion";
import { useRole } from "@/shared/auth/hooks/useRole";
import { useMediaQuery } from "@/shared/hooks/useMediaQuery";
import { isEditableTarget } from "@/shared/utils/keyboard";
import { useUiStore } from "@/shared/ui/state/ui-store";
import { Button } from "@/shared/ui/button";
import { SentiumMark } from "@/shared/ui/sentium-mark";
import { Tooltip } from "@/shared/ui/tooltip";

interface NavItemProps extends NavLinkItem {
  collapsed: boolean;
  busy: boolean;
}

function NavItem({ to, label, icon: Icon, end, collapsed, busy }: NavItemProps) {
  return (
    <Tooltip content={label} side="right" disabled={!collapsed}>
      <NavLink
        to={to}
        end={end}
        className={({ isActive }) => (isActive ? `${styles.link} ${styles.active}` : styles.link)}
      >
        {({ isActive }) => (
          <>
            {isActive && <SharedLayoutIndicator layoutId="nav-active-pill" className={styles.activePill} />}
            <Icon size={18} strokeWidth={1.75} className={styles.icon} aria-hidden="true" />
            <span className={styles.label}>{label}</span>
            {busy && (
              <span className={styles.activity}>
                <span className="visually-hidden">running</span>
              </span>
            )}
          </>
        )}
      </NavLink>
    </Tooltip>
  );
}

interface NavSectionProps {
  group: NavGroup;
  links: NavLinkItem[];
  collapsed: boolean;
  open: boolean;
  busyRoutes: Set<string>;
  onToggle: () => void;
}

function NavSection({ group, links, collapsed, open, busyRoutes, onToggle }: NavSectionProps) {
  const listId = useId();
  // The rail has no room for headers, so it always lists every link with a hairline between groups.
  const expanded = collapsed || open;

  return (
    <div className={styles.group}>
      {collapsed ? (
        <span className={styles.groupDivider} aria-hidden="true" />
      ) : (
        <button
          type="button"
          className={styles.groupToggle}
          aria-expanded={open}
          aria-controls={listId}
          onClick={onToggle}
        >
          {group.label}
          <ChevronDown size={13} className={styles.groupChevron} aria-hidden="true" />
        </button>
      )}
      {expanded && (
        <div id={listId} className={styles.groupLinks}>
          {links.map((link) => (
            <NavItem key={link.to} {...link} collapsed={collapsed} busy={busyRoutes.has(link.to)} />
          ))}
        </div>
      )}
    </div>
  );
}

function useBusyRoutes(): Set<string> {
  const assistantStreaming = useConversationStore((s) => s.isStreaming);
  const orchestrationRunning = useOrchestrationRunStore((s) => s.isRunning);
  const timelineChatStreaming = useTimelineChatStore((s) => s.isStreaming);

  const busyRoutes = new Set<string>();
  if (assistantStreaming) {
    busyRoutes.add("/assistant");
  }

  if (orchestrationRunning) {
    busyRoutes.add("/orchestration");
  }

  if (timelineChatStreaming) {
    busyRoutes.add("/timeline");
  }

  return busyRoutes;
}

export function Navbar() {
  const { isSovereign } = useRole();
  const { pathname } = useLocation();
  const busyRoutes = useBusyRoutes();

  const storedCollapsed = useUiStore((s) => s.sidebarCollapsed);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const collapsedGroups = useUiStore((s) => s.collapsedNavGroups);
  const toggleNavGroup = useUiStore((s) => s.toggleNavGroup);
  const isNarrow = useMediaQuery("(max-width: 1099px)");
  const collapsed = isNarrow || storedCollapsed;
  // At phone width the layout shows the Companion as a floating bubble instead.
  const isPhone = useMediaQuery(PHONE_QUERY);
  const activeGroupId = findNavGroup(pathname)?.id;

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === "b" && !isEditableTarget(e.target)) {
        e.preventDefault();
        toggleSidebar();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [toggleSidebar]);

  const toggleLabel = collapsed ? "Expand sidebar" : "Collapse sidebar";

  return (
    <nav className={`frame ${styles.nav}`} data-collapsed={collapsed || undefined} aria-label="Main navigation">
      <div className={styles.brand}>
        <SentiumMark className={styles.logo} />
        <span className={styles.wordmark}>Sentium</span>
        {!isNarrow && (
          <Tooltip content={`${toggleLabel} (Ctrl+B)`} side="right">
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              aria-label={toggleLabel}
              aria-keyshortcuts="Control+B Meta+B"
              onClick={toggleSidebar}
              icon={collapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
              className={styles.collapseToggle}
            />
          </Tooltip>
        )}
      </div>

      <div className={styles.scroll}>
        <div className={styles.groupLinks}>
          <NavItem {...HOME_LINK} collapsed={collapsed} busy={false} />
        </div>
        {NAV_GROUPS.map((group) => {
          const links = visibleLinks(group.links, isSovereign);
          if (links.length === 0) {
            return null;
          }
          return (
            <NavSection
              key={group.id}
              group={group}
              links={links}
              collapsed={collapsed}
              // The group holding the current page stays open so the active link is never hidden.
              open={group.id === activeGroupId || !collapsedGroups.includes(group.id)}
              busyRoutes={busyRoutes}
              onToggle={() => toggleNavGroup(group.id)}
            />
          );
        })}
      </div>

      <div className={styles.footer}>
        {!isPhone && <Companion placement="sidebar" collapsed={collapsed} />}
        <span className={styles.version}>v1.0.0</span>
      </div>
    </nav>
  );
}
