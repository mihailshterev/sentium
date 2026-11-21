export type PaletteId = "warm" | "cosmos" | "graphite" | "forest" | "aubergine" | "ember";

/** A few representative colours of one mode, for the picker's preview. The real values live in the CSS tokens. */
export interface PaletteSwatch {
  base: string;
  surface: string;
  accent: string;
  text: string;
  hazeA: string;
  hazeB: string;
}

export interface Palette {
  id: PaletteId;
  label: string;
  description: string;
  swatches: { dark: PaletteSwatch; light: PaletteSwatch };
}

export const DEFAULT_PALETTE: PaletteId = "warm";

const swatch = (base: string, surface: string, accent: string, text: string, hazeA: string, hazeB: string) => ({
  base,
  surface,
  accent,
  text,
  hazeA,
  hazeB,
});

export const PALETTES: readonly Palette[] = [
  {
    id: "warm",
    label: "Warm charcoal",
    description: "Espresso black with a brass accent",
    swatches: {
      dark: swatch("#0a0806", "#12100d", "#e3b873", "#f0ebe3", "#2a1a10", "#24131c"),
      light: swatch("#f3efe8", "#fbf9f5", "#e3b873", "#1a1510", "#ecdcc8", "#eedad6"),
    },
  },
  {
    id: "cosmos",
    label: "Cosmos",
    description: "The original deep-space blue with teal-green",
    swatches: {
      dark: swatch("#05070c", "#0a0e17", "#7fd8b4", "#e9ecf2", "#0b2a3a", "#1e1640"),
      light: swatch("#eef1f5", "#f9fafc", "#7fd8b4", "#10141f", "#d3e6ec", "#dfdaf1"),
    },
  },
  {
    id: "graphite",
    label: "Graphite",
    description: "Neutral grey, monochrome accent",
    swatches: {
      dark: swatch("#070707", "#0f0f10", "#e4e4e7", "#ececed", "#18181a", "#141416"),
      light: swatch("#f2f2f3", "#fbfbfb", "#18181b", "#111113", "#e3e3e6", "#e8e8ea"),
    },
  },
  {
    id: "forest",
    label: "Forest",
    description: "Green-black with a moss accent",
    swatches: {
      dark: swatch("#040806", "#09100c", "#9fd18b", "#e8f0eb", "#0c2a1e", "#1a2412"),
      light: swatch("#eef2ee", "#f9fbf8", "#9fd18b", "#0f1a13", "#d6e8d9", "#e3e8d2"),
    },
  },
  {
    id: "aubergine",
    label: "Aubergine",
    description: "Plum black with a lilac accent",
    swatches: {
      dark: swatch("#08060b", "#100c15", "#c9a7f5", "#efe9f2", "#1e1640", "#2b1020"),
      light: swatch("#f3eff6", "#fbf9fc", "#c9a7f5", "#18101f", "#e4daf1", "#f0dbe5"),
    },
  },
  {
    id: "ember",
    label: "Ember",
    description: "Oxblood dark with a peach-copper accent",
    swatches: {
      dark: swatch("#0c0605", "#150c0a", "#f0b99a", "#f3e8e4", "#3a140c", "#2a0f18"),
      light: swatch("#f6efec", "#fcf8f6", "#f0b99a", "#1f110c", "#f0d8cc", "#ecd6dc"),
    },
  },
];

export const isPaletteId = (value: unknown): value is PaletteId => PALETTES.some((p) => p.id === value);
