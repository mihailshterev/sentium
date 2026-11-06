/** Geometry for the orbital map, in SVG user units of an 800 × 640 view box centred on (400, 320). */

export const VIEW_W = 800;
export const VIEW_H = 640;
export const CX = VIEW_W / 2;
export const CY = VIEW_H / 2;

export const R_ORB = 150;
export const R_RIM = 164;
export const R_INNER = 196;
export const R_TICKS = 230;
export const R_OUTER = 262;
export const R_EDGE = 296;
/** Half side of the square reticle that frames the rings. */
export const RETICLE = 300;

export interface Point {
  x: number;
  y: number;
}

export type LabelSide = "right" | "left" | "top" | "bottom";

export interface PlacedNode<T> {
  node: T;
  deg: number;
  point: Point;
  /** Anchor for the node's label, just outside the node along its radius. */
  labelPoint: Point;
  side: LabelSide;
}

const rad = (deg: number) => (deg * Math.PI) / 180;

/** Point at radius r and angle deg (0° = east, clockwise, as SVG's y axis points down). */
export function polar(r: number, deg: number): Point {
  return { x: CX + r * Math.cos(rad(deg)), y: CY + r * Math.sin(rad(deg)) };
}

/** Which side of its node a label sits on, so labels always point away from the centre. */
export function labelSide(deg: number): LabelSide {
  const cos = Math.cos(rad(deg));
  if (cos > 0.3) {
    return "right";
  }
  if (cos < -0.3) {
    return "left";
  }
  return Math.sin(rad(deg)) < 0 ? "top" : "bottom";
}

/** Spreads nodes evenly around a ring, starting at startDeg. */
export function placeRing<T>(nodes: readonly T[], r: number, startDeg: number, labelGap = 14): PlacedNode<T>[] {
  const step = 360 / Math.max(nodes.length, 1);
  return nodes.map((node, i) => {
    const deg = startDeg + i * step;
    return { node, deg, point: polar(r, deg), labelPoint: polar(r + labelGap, deg), side: labelSide(deg) };
  });
}

/** Smallest absolute difference between two angles, in degrees (0-180). */
export function angleDistance(a: number, b: number): number {
  const d = Math.abs((((a - b) % 360) + 360) % 360);
  return d > 180 ? 360 - d : d;
}

/** Index of the placed node whose angle is closest to deg, or -1 when there are none. */
export function nearestByAngle(placed: readonly { deg: number }[], deg: number): number {
  let best = -1;
  let bestDistance = Infinity;
  placed.forEach((p, i) => {
    const d = angleDistance(p.deg, deg);
    if (d < bestDistance) {
      best = i;
      bestDistance = d;
    }
  });
  return best;
}

/** CSS position of a view-box point inside the map's box. */
export function toPercent(p: Point): { left: string; top: string } {
  return { left: `${(p.x / VIEW_W) * 100}%`, top: `${(p.y / VIEW_H) * 100}%` };
}

/** Lower-case, dash-separated identifier for compact technical labels. */
export function toSlug(value: string, max = 16): string {
  const slug = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug.length > max ? `${slug.slice(0, max - 1)}…` : slug;
}
