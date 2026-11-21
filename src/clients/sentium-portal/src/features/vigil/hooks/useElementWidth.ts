import { useLayoutEffect, useRef, useState } from "react";

/** Tracks an element's content width; `fallback` is used until it is measured or where ResizeObserver is missing. */
export function useElementWidth<T extends HTMLElement>(fallback: number) {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(fallback);

  useLayoutEffect(() => {
    const element = ref.current;
    if (!element || typeof ResizeObserver === "undefined") {
      return;
    }

    const observer = new ResizeObserver(([entry]) => {
      const measured = Math.floor(entry.contentRect.width);
      if (measured > 0) {
        setWidth(measured);
      }
    });

    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return { ref, width };
}
