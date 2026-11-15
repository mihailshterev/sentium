import { useCallback, useId, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Check, ChevronDown } from "lucide-react";
import controlStyles from "./form-controls.module.css";
import styles from "./select.module.css";

const cx = (...classes: (string | false | null | undefined)[]) => classes.filter(Boolean).join(" ");

const MENU_MAX_HEIGHT = 280;
const MENU_GAP = 4;
const VIEWPORT_MARGIN = 8;

type SelectProps = React.SelectHTMLAttributes<HTMLSelectElement> & {
  /** Shows the error state and sets aria-invalid. */
  invalid?: boolean;
  controlSize?: "sm" | "md";
  ref?: React.Ref<HTMLSelectElement>;
  wrapperClassName?: string;
};

interface MenuOption {
  index: number;
  label: string;
  disabled: boolean;
}

/**
 * A native `<select>` as the trigger with a styled popup menu (the same look as the ModelSelector chip menu).
 * The select stays the source of truth, so labels, react-hook-form `register`, form posts and Playwright
 * `selectOption` keep working. Touch input and `multiple`/`size` selects keep the native picker.
 */
export function Select({
  invalid,
  controlSize = "md",
  className,
  wrapperClassName,
  children,
  ref,
  onMouseDown,
  onPointerDown,
  onKeyDown,
  onBlur,
  ...rest
}: SelectProps) {
  const selectRef = useRef<HTMLSelectElement | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const lastPointerType = useRef("mouse");
  const menuId = useId();
  const [options, setOptions] = useState<MenuOption[] | null>(null);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const open = options !== null;
  const nativePicker = rest.multiple || (rest.size ?? 0) > 1;

  const setRefs = useCallback(
    (node: HTMLSelectElement | null) => {
      selectRef.current = node;
      if (typeof ref === "function") {
        return ref(node);
      }
      if (ref) {
        ref.current = node;
      }
    },
    [ref],
  );

  const close = () => setOptions(null);

  const openMenu = () => {
    const select = selectRef.current;
    if (!select || select.disabled) {
      return;
    }
    const items = readOptions(select);
    setOptions(items);
    setSelectedIndex(select.selectedIndex);
    setActiveIndex(items.some((o) => o.index === select.selectedIndex) ? select.selectedIndex : firstEnabled(items));
  };

  const commit = (index: number) => {
    const select = selectRef.current;
    close();
    if (!select || select.selectedIndex === index) {
      return;
    }
    select.selectedIndex = index;
    select.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLSelectElement>) => {
    lastPointerType.current = e.pointerType;
    onPointerDown?.(e);
  };

  const handleMouseDown = (e: React.MouseEvent<HTMLSelectElement>) => {
    onMouseDown?.(e);
    if (e.defaultPrevented || e.button !== 0 || nativePicker || lastPointerType.current === "touch") {
      return;
    }
    e.preventDefault();
    e.currentTarget.focus();
    if (open) {
      close();
    } else {
      openMenu();
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLSelectElement>) => {
    onKeyDown?.(e);
    if (e.defaultPrevented || nativePicker) {
      return;
    }
    if (!open) {
      if (e.key === " " || e.key === "Enter" || e.key === "F4" || (e.altKey && e.key === "ArrowDown")) {
        e.preventDefault();
        openMenu();
      }
      return;
    }
    handleMenuKey(e, options);
  };

  const handleMenuKey = (e: React.KeyboardEvent<HTMLSelectElement>, items: MenuOption[]) => {
    const enabled = items.filter((o) => !o.disabled);
    const position = enabled.findIndex((o) => o.index === activeIndex);
    const moveTo = (next: MenuOption | undefined) => {
      e.preventDefault();
      if (next) {
        setActiveIndex(next.index);
      }
    };

    switch (e.key) {
      case "ArrowDown":
        return moveTo(enabled[Math.min(position + 1, enabled.length - 1)]);
      case "ArrowUp":
        return moveTo(enabled[Math.max(position - 1, 0)]);
      case "Home":
      case "PageUp":
        return moveTo(enabled[0]);
      case "End":
      case "PageDown":
        return moveTo(enabled[enabled.length - 1]);
      case "Enter":
      case " ":
        e.preventDefault();
        return activeIndex >= 0 ? commit(activeIndex) : close();
      case "Escape":
        // Keeps an enclosing Dialog (which listens on document) from closing too.
        e.preventDefault();
        e.stopPropagation();
        return close();
      case "Tab":
        return close();
      default:
        if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
          moveTo(typeAheadMatch(enabled, position, e.key));
        }
    }
  };

  const handleBlur = (e: React.FocusEvent<HTMLSelectElement>) => {
    close();
    onBlur?.(e);
  };

  useLayoutEffect(() => {
    if (!open) {
      return;
    }
    const place = () => {
      const select = selectRef.current;
      const menu = menuRef.current;
      if (select && menu) {
        positionMenu(select, menu);
      }
    };
    place();
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
    };
  }, [open]);

  useLayoutEffect(() => {
    menuRef.current
      ?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)
      ?.scrollIntoView?.({ block: "nearest" });
  }, [activeIndex, open]);

  return (
    <span className={cx(styles.selectWrap, wrapperClassName)}>
      <select
        ref={setRefs}
        className={cx(
          controlStyles.control,
          controlStyles[controlSize],
          styles.select,
          invalid && controlStyles.invalid,
          open && styles.selectOpen,
          className,
        )}
        aria-invalid={invalid || undefined}
        aria-expanded={nativePicker ? undefined : open}
        aria-controls={open ? menuId : undefined}
        onPointerDown={handlePointerDown}
        onMouseDown={handleMouseDown}
        onKeyDown={handleKeyDown}
        onBlur={handleBlur}
        {...rest}
      >
        {children}
      </select>
      <ChevronDown size={15} className={cx(styles.chevron, open && styles.chevronOpen)} aria-hidden="true" />
      {open &&
        createPortal(
          <div
            ref={menuRef}
            id={menuId}
            className={cx(styles.menu, controlSize === "sm" && styles.menuSm)}
            role="presentation"
            onMouseDown={(e) => e.preventDefault()}
          >
            {options.map((o) => {
              const selected = o.index === selectedIndex;
              return (
                <div
                  key={o.index}
                  data-index={o.index}
                  className={cx(
                    styles.option,
                    selected && styles.optionSelected,
                    o.index === activeIndex && styles.optionActive,
                    o.disabled && styles.optionDisabled,
                  )}
                  onMouseEnter={() => !o.disabled && setActiveIndex(o.index)}
                  onClick={() => !o.disabled && commit(o.index)}
                >
                  <Check size={13} className={selected ? styles.check : styles.checkHidden} aria-hidden="true" />
                  <span className={styles.optionLabel}>{o.label}</span>
                </div>
              );
            })}
          </div>,
          document.body,
        )}
    </span>
  );
}

function readOptions(select: HTMLSelectElement): MenuOption[] {
  return [...select.options]
    .map((o, index) => ({ option: o, index }))
    .filter(({ option }) => !option.hidden)
    .map(({ option, index }) => ({
      index,
      label: option.label || option.text,
      disabled:
        option.disabled || (option.parentElement instanceof HTMLOptGroupElement && option.parentElement.disabled),
    }));
}

function firstEnabled(items: MenuOption[]): number {
  return items.find((o) => !o.disabled)?.index ?? -1;
}

/** Next option after the current one whose label starts with `key`, wrapping around. */
function typeAheadMatch(enabled: MenuOption[], position: number, key: string): MenuOption | undefined {
  const needle = key.toLocaleLowerCase();
  const ordered = [...enabled.slice(position + 1), ...enabled.slice(0, position + 1)];
  return ordered.find((o) => o.label.toLocaleLowerCase().startsWith(needle));
}

/** Fixed-positions the menu under the select, flipping above it when there is more room there. */
function positionMenu(select: HTMLSelectElement, menu: HTMLDivElement) {
  const rect = select.getBoundingClientRect();
  const spaceBelow = window.innerHeight - rect.bottom - MENU_GAP - VIEWPORT_MARGIN;
  const spaceAbove = rect.top - MENU_GAP - VIEWPORT_MARGIN;
  const wanted = Math.min(menu.scrollHeight, MENU_MAX_HEIGHT);
  const below = spaceBelow >= wanted || spaceBelow >= spaceAbove;

  menu.style.minWidth = `${rect.width}px`;
  menu.style.maxHeight = `${Math.max(0, Math.min(MENU_MAX_HEIGHT, below ? spaceBelow : spaceAbove))}px`;
  menu.style.top = below ? `${rect.bottom + MENU_GAP}px` : "";
  menu.style.bottom = below ? "" : `${window.innerHeight - rect.top + MENU_GAP}px`;

  const maxLeft = window.innerWidth - menu.offsetWidth - VIEWPORT_MARGIN;
  menu.style.left = `${Math.max(VIEWPORT_MARGIN, Math.min(rect.left, maxLeft))}px`;
}
