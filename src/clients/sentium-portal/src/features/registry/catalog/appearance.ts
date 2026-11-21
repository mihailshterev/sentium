import { Palette } from "lucide-react";
import { PALETTES } from "@/shared/ui/state/palettes";

export const APPEARANCE_SECTION_ID = "appearance";
export const APPEARANCE_TITLE = "Appearance";
export const APPEARANCE_ICON = Palette;

const SEARCH_TERMS = "appearance theme palette colour color mode dark light system";

/** Whether the Appearance section survives the settings search, using the same every-term rule as the settings. */
export function matchesAppearanceSearch(query: string): boolean {
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  const haystack = [SEARCH_TERMS, ...PALETTES.map((p) => p.label)].join(" ").toLowerCase();
  return terms.every((term) => haystack.includes(term));
}
