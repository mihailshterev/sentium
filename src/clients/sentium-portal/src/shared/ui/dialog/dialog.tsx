import { useEffect, useEffectEvent, useId, useRef } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { Button } from "@/shared/ui/button";
import styles from "./dialog.module.css";

type DialogSize = "sm" | "md" | "lg";
type DialogTone = "accent" | "danger" | "success" | "info";

interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ReactNode;
  iconTone?: DialogTone;
  /** Action row; for forms either put the buttons inside the form or give them `form="<form id>"`. */
  footer?: React.ReactNode;
  size?: DialogSize;
  /** Escape, backdrop click and the close button dismiss the dialog (default true). */
  dismissible?: boolean;
  hideCloseButton?: boolean;
  /** Element focused on open; defaults to the first focusable element. */
  initialFocus?: React.RefObject<HTMLElement | null>;
  className?: string;
  children?: React.ReactNode;
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** Modal dialog on thick glass, rendered in a portal with focus trapping and focus restore. */
export function Dialog({
  open,
  onClose,
  title,
  description,
  icon,
  iconTone = "accent",
  footer,
  size = "md",
  dismissible = true,
  hideCloseButton = false,
  initialFocus,
  className,
  children,
}: DialogProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const titleId = useId();
  const descId = useId();

  const requestClose = useEffectEvent(() => {
    if (dismissible) {
      onClose();
    }
  });

  const focusInitial = useEffectEvent(() => {
    const panel = panelRef.current;
    const first = panel?.querySelector<HTMLElement>(FOCUSABLE);
    (initialFocus?.current ?? first ?? panel)?.focus();
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    const previouslyFocused = document.activeElement as HTMLElement | null;
    focusInitial();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        requestClose();
        return;
      }
      if (e.key !== "Tab" || !panelRef.current) {
        return;
      }
      const items = [...panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE)];
      if (items.length === 0) {
        e.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("keydown", onKey);
      previouslyFocused?.focus?.();
    };
  }, [open]);

  if (!open) {
    return null;
  }

  return createPortal(
    <div className={styles.modalOverlay} onClick={(e) => dismissible && e.target === e.currentTarget && onClose()}>
      <div
        ref={panelRef}
        className={["frame", styles.modal, styles[size], className ?? ""].filter(Boolean).join(" ")}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descId : undefined}
        tabIndex={-1}
      >
        <div className={styles.header}>
          {icon && (
            <span className={`${styles.icon} ${styles[iconTone]}`} aria-hidden="true">
              {icon}
            </span>
          )}
          <div className={styles.titles}>
            <h2 id={titleId} className={styles.title}>
              {title}
            </h2>
            {description && (
              <p id={descId} className={styles.description}>
                {description}
              </p>
            )}
          </div>
          {dismissible && !hideCloseButton && (
            <Button
              variant="ghost"
              size="sm"
              iconOnly
              aria-label="Close"
              onClick={onClose}
              icon={<X size={16} />}
              className={styles.close}
            />
          )}
        </div>
        {children != null && <div className={styles.body}>{children}</div>}
        {footer && <div className={styles.footer}>{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}
