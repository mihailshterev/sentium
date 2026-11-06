import { useId } from "react";
import { MotionConfig, motion } from "framer-motion";
import styles from "./tabs.module.css";

export interface TabItem {
  id: string;
  label: string;
  icon?: React.ReactNode;
  count?: number;
  testId?: string;
}

interface TabsProps {
  tabs: TabItem[];
  active: string;
  onChange: (id: string) => void;
  /** Accessible name for the tab list. */
  label?: string;
  className?: string;
}

/** Segmented control with a sliding selection indicator. Arrow keys move between tabs. */
export function Tabs({ tabs, active, onChange, label, className }: TabsProps) {
  const indicatorId = useId();

  const onKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    const index = tabs.findIndex((t) => t.id === active);
    const next =
      e.key === "ArrowRight"
        ? (index + 1) % tabs.length
        : e.key === "ArrowLeft"
          ? (index - 1 + tabs.length) % tabs.length
          : e.key === "Home"
            ? 0
            : e.key === "End"
              ? tabs.length - 1
              : -1;
    if (next < 0) {
      return;
    }
    e.preventDefault();
    onChange(tabs[next].id);
    (e.currentTarget.querySelectorAll<HTMLButtonElement>("[role=tab]")[next] ?? null)?.focus();
  };

  return (
    <MotionConfig reducedMotion="user">
      <div
        className={[styles.tabs, className ?? ""].filter(Boolean).join(" ")}
        role="tablist"
        aria-label={label}
        onKeyDown={onKeyDown}
      >
        {tabs.map((tab) => {
          const selected = active === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={selected}
              tabIndex={selected ? 0 : -1}
              className={`${styles.tab} ${selected ? styles.active : ""}`}
              onClick={() => onChange(tab.id)}
              data-testid={tab.testId}
            >
              {selected && (
                <motion.span
                  layoutId={indicatorId}
                  className={styles.indicator}
                  transition={{ type: "spring", stiffness: 520, damping: 40 }}
                />
              )}
              {tab.icon && <span className={styles.icon}>{tab.icon}</span>}
              <span className={styles.text}>{tab.label}</span>
              {tab.count != null && <span className={styles.count}>{tab.count}</span>}
            </button>
          );
        })}
      </div>
    </MotionConfig>
  );
}
