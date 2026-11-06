import { parseColor, type Rgb } from "./parse-color";

/** Reads a CSS custom property from :root as 0..1 RGB, with a fallback for unparseable values. */
export function readCssColor(name: string, fallback: Rgb): Rgb {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name);
  return parseColor(value) ?? fallback;
}

/** Reads a numeric CSS custom property from :root. */
export function readCssNumber(name: string, fallback: number): number {
  const value = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue(name));
  return Number.isFinite(value) ? value : fallback;
}
