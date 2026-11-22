import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useNavigate } from "react-router";
import { CornerDownLeft, MessageSquare, Search } from "lucide-react";
import {
  ACCOUNT_LINKS,
  HOME_LINK,
  NAV_GROUPS,
  QUICK_ACTIONS,
  visibleLinks,
  type NavLinkItem,
} from "../navbar/nav-config";
import { COMMAND_PALETTE_SHORTCUT, OPEN_COMMAND_PALETTE_EVENT } from "./open-command-palette";
import { useConversations } from "@/features/assistant/hooks/useConversations";
import { useRole } from "@/shared/auth/hooks/useRole";
import { useDebouncedValue } from "@/shared/hooks/useDebouncedValue";
import { formatRelativeTime } from "@/shared/utils/formatters";
import styles from "./command-palette.module.css";

const RECENT_CONVERSATIONS = 4;
const MATCHING_CONVERSATIONS = 8;

interface PaletteEntry {
  id: string;
  section: string;
  label: string;
  hint?: string;
  icon: React.ComponentType<{ size?: number; "aria-hidden"?: boolean }>;
  to: string;
}

const matches = (link: NavLinkItem, query: string, groupLabel = "") =>
  [link.label, link.description, groupLabel].some((text) => text.toLowerCase().includes(query));

/**
 * Ctrl/Cmd+K: jump to any page, run a quick action or reopen a conversation. Text fields that bind Ctrl+K
 * themselves keep it by calling preventDefault first.
 */
export function CommandPalette() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const show = () => setOpen(true);
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === "k" && !e.defaultPrevented) {
        e.preventDefault();
        setOpen((o) => !o);
      }
    };
    window.addEventListener(OPEN_COMMAND_PALETTE_EVENT, show);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener(OPEN_COMMAND_PALETTE_EVENT, show);
      window.removeEventListener("keydown", onKey);
    };
  }, []);

  if (!open) {
    return null;
  }
  return createPortal(<PalettePanel onClose={() => setOpen(false)} />, document.body);
}

function PalettePanel({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const { isSovereign } = useRole();
  const inputRef = useRef<HTMLInputElement>(null);
  const listId = useId();
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const term = query.trim().toLowerCase();
  const { conversations } = useConversations(useDebouncedValue(term, 200));

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    inputRef.current?.focus();
    return () => previouslyFocused?.focus?.();
  }, []);

  const entries: PaletteEntry[] = [];
  const add = (section: string, links: NavLinkItem[], groupLabel?: string) => {
    for (const link of visibleLinks(links, isSovereign)) {
      if (!term || matches(link, term, groupLabel)) {
        entries.push({
          id: `${section}:${link.to}`,
          section,
          label: link.label,
          hint: link.description,
          icon: link.icon,
          to: link.to,
        });
      }
    }
  };

  add("Actions", QUICK_ACTIONS);
  add("Pages", [HOME_LINK]);
  for (const group of NAV_GROUPS) {
    add("Pages", group.links, group.label);
  }
  add("Pages", ACCOUNT_LINKS);
  for (const conversation of conversations.slice(0, term ? MATCHING_CONVERSATIONS : RECENT_CONVERSATIONS)) {
    entries.push({
      id: `conversation:${conversation.id}`,
      section: term ? "Conversations" : "Recent conversations",
      label: conversation.title || "Untitled conversation",
      hint: formatRelativeTime(conversation.createdAt),
      icon: MessageSquare,
      to: `/assistant/${conversation.id}`,
    });
  }

  const activeIndex = Math.min(active, Math.max(entries.length - 1, 0));
  const optionId = (index: number) => `${listId}-option-${index}`;

  useEffect(() => {
    document.getElementById(optionId(activeIndex))?.scrollIntoView?.({ block: "nearest" });
  });

  const choose = (entry: PaletteEntry | undefined) => {
    if (!entry) {
      return;
    }
    onClose();
    navigate(entry.to);
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    const step = (delta: number) => setActive((activeIndex + delta + entries.length) % Math.max(entries.length, 1));

    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        step(1);
        break;
      case "ArrowUp":
        e.preventDefault();
        step(-1);
        break;
      case "Enter":
        e.preventDefault();
        choose(entries[activeIndex]);
        break;
      case "Escape":
        e.preventDefault();
        onClose();
        break;
      case "Tab":
        // The input is the only focusable element; options are reached with the arrow keys.
        e.preventDefault();
        break;
    }
  };

  return (
    <div className={styles.overlay} onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div
        className={`frame ${styles.panel}`}
        role="dialog"
        aria-modal="true"
        aria-label="Command palette"
        onKeyDown={onKeyDown}
      >
        <div className={styles.inputRow}>
          <Search size={16} aria-hidden="true" />
          <input
            ref={inputRef}
            className={styles.input}
            placeholder="Search pages, actions and conversations…"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setActive(0);
            }}
            role="combobox"
            aria-expanded="true"
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={entries.length > 0 ? optionId(activeIndex) : undefined}
            aria-label="Search pages, actions and conversations"
          />
          <kbd className={styles.kbd}>Esc</kbd>
        </div>

        <div id={listId} role="listbox" aria-label="Results" className={styles.list}>
          {entries.length === 0 && <p className={styles.empty}>No matches for “{query.trim()}”</p>}
          {entries.map((entry, index) => {
            const Icon = entry.icon;
            const startsSection = index === 0 || entries[index - 1].section !== entry.section;
            return (
              <div key={entry.id} role="presentation">
                {startsSection && (
                  <div className={styles.section} role="presentation">
                    {entry.section}
                  </div>
                )}
                <div
                  id={optionId(index)}
                  role="option"
                  aria-selected={index === activeIndex}
                  className={styles.option}
                  onMouseMove={() => index !== activeIndex && setActive(index)}
                  onClick={() => choose(entry)}
                >
                  <Icon size={16} aria-hidden={true} />
                  <span className={styles.label}>{entry.label}</span>
                  {entry.hint && <span className={styles.hint}>{entry.hint}</span>}
                  {index === activeIndex && <CornerDownLeft size={14} className={styles.enter} aria-hidden="true" />}
                </div>
              </div>
            );
          })}
        </div>

        <div className={styles.footer} aria-hidden="true">
          <span>
            <kbd className={styles.kbd}>↑</kbd> <kbd className={styles.kbd}>↓</kbd> to move
          </span>
          <span>
            <kbd className={styles.kbd}>↵</kbd> to open
          </span>
          <span>
            <kbd className={styles.kbd}>{COMMAND_PALETTE_SHORTCUT}</kbd> from anywhere
          </span>
        </div>
      </div>
    </div>
  );
}
