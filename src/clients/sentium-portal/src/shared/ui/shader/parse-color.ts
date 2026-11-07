export type Rgb = [number, number, number];

/** Parses `#rgb`, `#rrggbb` or `rgb()/rgba()` into 0..1 channels. */
export function parseColor(value: string): Rgb | null {
  const v = value.trim();
  const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(v);
  if (hex) {
    const h =
      hex[1].length === 3
        ? hex[1]
            .split("")
            .map((c) => c + c)
            .join("")
        : hex[1];
    return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255) as Rgb;
  }
  const rgb = /^rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)/i.exec(v);
  if (rgb) {
    return [rgb[1], rgb[2], rgb[3]].map((n) => Number(n) / 255) as Rgb;
  }
  return null;
}
