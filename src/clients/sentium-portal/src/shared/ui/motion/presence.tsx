import { isValidElement, useState, type ReactElement, type ReactNode } from "react";
import { PresenceContext } from "./presence-context";

interface PresenceProps {
  /** At most one keyed element; changing its key exits the old one and enters the new one. */
  children?: ReactNode;
  /** Whether a child present on the first render plays its enter animation. */
  initial?: boolean;
  popLayout?: boolean;
}

interface PresenceItem {
  key: string;
  element: ReactElement;
  present: boolean;
  animateEnter: boolean;
}

/**
 * Keeps a removed child mounted until its exit animation (an {@link Animated} inside it) has finished, so elements
 * can animate out as well as in.
 */
export function Presence({ children, initial = true, popLayout = false }: PresenceProps) {
  const child = isValidElement(children) ? children : null;
  const [items, setItems] = useState<PresenceItem[]>(() =>
    child ? [{ key: keyOf(child), element: child, present: true, animateEnter: initial }] : [],
  );

  // Adjusting state while rendering: React re-renders straight away, before anything reaches the screen.
  const reconciled = reconcile(items, child);
  if (reconciled !== items) {
    setItems(reconciled);
  }

  const complete = (key: string) =>
    setItems((current) => current.filter((item) => item.key !== key || item.present));

  return reconciled.map((item) => (
    <PresenceContext.Provider
      key={item.key}
      value={{
        isPresent: item.present,
        animateEnter: item.animateEnter,
        popLayout,
        onExitComplete: () => complete(item.key),
      }}
    >
      {item.element}
    </PresenceContext.Provider>
  ));
}

function keyOf(element: ReactElement): string {
  return String(element.key ?? "");
}

/** The items for the current child, or the same array when nothing changed. */
function reconcile(items: PresenceItem[], child: ReactElement | null): PresenceItem[] {
  const childKey = child ? keyOf(child) : null;
  const unchanged = items.every((item) =>
    item.key === childKey ? item.present && item.element === child : !item.present,
  );
  if (unchanged && (child === null || items.some((item) => item.key === childKey))) {
    return items;
  }

  // Items appearing after the first render always animate in.
  const next = items.map((item) =>
    item.key === childKey && child ? { ...item, element: child, present: true } : { ...item, present: false },
  );
  if (child && childKey !== null && !next.some((item) => item.key === childKey)) {
    next.push({ key: childKey, element: child, present: true, animateEnter: true });
  }
  return next;
}
