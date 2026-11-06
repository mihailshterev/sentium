import { useState } from "react";
import { PageHeader } from "@/shared/ui/page-header";
import styles from "./page.module.css";

export type PageWidth = "default" | "wide" | "full";

interface PageProps {
  title: string;
  subtitle?: React.ReactNode;
  /** Mono path label above the title. */
  eyebrow?: string;
  icon?: React.ReactNode;
  /** Interactive element before the title, e.g. a back button. */
  leading?: React.ReactNode;
  /** Header actions, right-aligned. */
  actions?: React.ReactNode;
  width?: PageWidth;
  /**
   * `true` (default): the body scrolls and the header turns to glass once content passes under it.
   * `false`: full-height app pages (chat, builders, maps) that manage their own panes and scrolling.
   */
  scroll?: boolean;
  className?: string;
  bodyClassName?: string;
  children: React.ReactNode;
}

/** Standard page frame: header + body with consistent gutters, max-width and scroll behaviour. */
export function Page({
  title,
  subtitle,
  eyebrow,
  icon,
  leading,
  actions,
  width = "default",
  scroll = true,
  className,
  bodyClassName,
  children,
}: PageProps) {
  const [scrolled, setScrolled] = useState(false);

  return (
    <div className={[styles.page, className ?? ""].filter(Boolean).join(" ")}>
      <PageHeader
        title={title}
        subtitle={subtitle}
        eyebrow={eyebrow}
        icon={icon}
        leading={leading}
        right={actions}
        width={width}
        scrolled={scrolled}
      />
      {scroll ? (
        <div className={styles.scroll} onScroll={(e) => setScrolled(e.currentTarget.scrollTop > 4)}>
          <div className={[styles.body, bodyClassName ?? ""].filter(Boolean).join(" ")} data-width={width}>
            {children}
          </div>
        </div>
      ) : (
        <div className={[styles.fixedBody, bodyClassName ?? ""].filter(Boolean).join(" ")} data-width={width}>
          {children}
        </div>
      )}
    </div>
  );
}
