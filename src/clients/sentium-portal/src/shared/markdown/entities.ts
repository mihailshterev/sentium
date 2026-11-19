const REPLACEMENT_CHARACTER = "\uFFFD";
const MAX_CODE_POINT = 0x10ffff;

// The named references people and models actually type. An unknown name is left as literal text, never guessed.
const NAMED: Record<string, string> = {
  amp: "&",
  lt: "<",
  gt: ">",
  quot: '"',
  apos: "'",
  nbsp: "\u00A0",
  ensp: "\u2002",
  emsp: "\u2003",
  thinsp: "\u2009",
  shy: "\u00AD",
  copy: "©",
  reg: "®",
  trade: "™",
  deg: "°",
  plusmn: "±",
  times: "×",
  divide: "÷",
  micro: "µ",
  para: "¶",
  sect: "§",
  middot: "·",
  bull: "•",
  hellip: "…",
  ndash: "–",
  mdash: "—",
  lsquo: "‘",
  rsquo: "’",
  sbquo: "‚",
  ldquo: "“",
  rdquo: "”",
  bdquo: "„",
  laquo: "«",
  raquo: "»",
  lsaquo: "‹",
  rsaquo: "›",
  larr: "←",
  rarr: "→",
  uarr: "↑",
  darr: "↓",
  harr: "↔",
  lArr: "⇐",
  rArr: "⇒",
  hArr: "⇔",
  le: "≤",
  ge: "≥",
  ne: "≠",
  asymp: "≈",
  infin: "∞",
  minus: "−",
  check: "✓",
  cross: "✗",
  euro: "€",
  pound: "£",
  yen: "¥",
  cent: "¢",
  frac12: "½",
  frac14: "¼",
  frac34: "¾",
  sup2: "²",
  sup3: "³",
  iexcl: "¡",
  iquest: "¿",
  dagger: "†",
  Dagger: "‡",
  permil: "‰",
  prime: "′",
  Prime: "″",
};

/** Decodes one `&...;` reference, or returns null when it is not one this renderer knows. */
export function decodeEntity(entity: string): string | null {
  const body = entity.slice(1, -1);
  if (body[0] !== "#") {
    return NAMED[body] ?? null;
  }

  const hex = body[1] === "x" || body[1] === "X";
  const code = parseInt(body.slice(hex ? 2 : 1), hex ? 16 : 10);
  const invalid = Number.isNaN(code) || code === 0 || code > MAX_CODE_POINT || (code >= 0xd800 && code <= 0xdfff);
  return invalid ? REPLACEMENT_CHARACTER : String.fromCodePoint(code);
}
