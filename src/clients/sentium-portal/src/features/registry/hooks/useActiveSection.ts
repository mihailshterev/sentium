import { useEffect, useState } from "react";

/** The page header overlays the scroll area; a section counts as current once its top passes under it. */
const ROOT_MARGIN = "-140px 0px -55% 0px";

/** Scroll-spy: the id of the top-most section element currently in the reading band. */
export function useActiveSection(ids: string[]) {
  const [active, setActive] = useState<string | null>(ids[0] ?? null);
  const key = ids.join("|");

  useEffect(() => {
    const elements = key
      .split("|")
      .map((id) => document.getElementById(id))
      .filter((el): el is HTMLElement => el !== null);
    if (elements.length === 0 || typeof IntersectionObserver === "undefined") {
      return;
    }

    const visible = new Set<string>();
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            visible.add(entry.target.id);
          } else {
            visible.delete(entry.target.id);
          }
        }
        const first = elements.find((el) => visible.has(el.id));
        if (first) {
          setActive(first.id);
        }
      },
      { rootMargin: ROOT_MARGIN },
    );
    elements.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, [key]);

  return [active, setActive] as const;
}
