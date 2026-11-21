import { readCssColor, type Rgb } from "@/shared/ui/shader";

/** The map's own surface colours as 0xRRGGBB, derived from the active palette's tokens. */
export interface MapColors {
  background: number;
  gridLine: number;
  gridDot: number;
  nebulaOuter: number;
  nebulaInner: number;
  edge: number;
  label: number;
  star: number;
}

// Warm charcoal (dark), used when the tokens can't be read, e.g. under jsdom.
const FALLBACK_BASE: Rgb = [10 / 255, 8 / 255, 6 / 255];
const FALLBACK_TEXT: Rgb = [240 / 255, 235 / 255, 227 / 255];
const FALLBACK_HAZE: Rgb = [42 / 255, 26 / 255, 16 / 255];
const FALLBACK_STAR: Rgb = [1, 243 / 255, 223 / 255];

// How far each surface colour moves from the background towards the text colour.
const GRID_LINE_MIX = { dark: 0.1, light: 0.14 };
const GRID_DOT_MIX = { dark: 0.22, light: 0.28 };
const EDGE_MIX = { dark: 0.3, light: 0.4 };
const NEBULA_OUTER_MIX = 0.6;
const NEBULA_INNER_LIFT = 0.08;

export function readMapColors(isLight: boolean): MapColors {
  const mode = isLight ? "light" : "dark";
  const base = readCssColor("--bg-base", FALLBACK_BASE);
  const text = readCssColor("--text-primary", FALLBACK_TEXT);
  const haze = readCssColor("--field-1", FALLBACK_HAZE);

  return {
    background: toHex(base),
    gridLine: toHex(mix(base, text, GRID_LINE_MIX[mode])),
    gridDot: toHex(mix(base, text, GRID_DOT_MIX[mode])),
    nebulaOuter: toHex(mix(base, haze, NEBULA_OUTER_MIX)),
    nebulaInner: toHex(mix(haze, text, NEBULA_INNER_LIFT)),
    edge: toHex(mix(base, text, EDGE_MIX[mode])),
    label: toHex(text),
    star: toHex(readCssColor("--field-star", FALLBACK_STAR)),
  };
}

function mix(from: Rgb, to: Rgb, amount: number): Rgb {
  return from.map((channel, i) => channel + (to[i] - channel) * amount) as Rgb;
}

function toHex([r, g, b]: Rgb): number {
  const byte = (channel: number) => Math.round(Math.min(1, Math.max(0, channel)) * 255);
  return (byte(r) << 16) | (byte(g) << 8) | byte(b);
}
