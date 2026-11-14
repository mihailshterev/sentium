/**
 * Geometry for the lattice core: two nested icosahedral shells turning against each other, with signal
 * pulses that travel the outer shell's edges from vertex to vertex. Everything is a pure function of time.
 */

export interface LatticeSegment {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  opacity: number;
}

export interface LatticeDot {
  x: number;
  y: number;
  r: number;
  opacity: number;
}

export interface LatticeFrame {
  outer: LatticeSegment[];
  inner: LatticeSegment[];
  vertices: LatticeDot[];
  /** Tail-to-head trail of each pulse; (x2, y2) is the head. */
  pulses: LatticeSegment[];
}

type Vec3 = readonly [number, number, number];

const PHI = (1 + Math.sqrt(5)) / 2;
const NORM = Math.hypot(1, PHI);

const RAW_VERTICES: Vec3[] = [
  [0, 1, PHI],
  [0, 1, -PHI],
  [0, -1, PHI],
  [0, -1, -PHI],
  [1, PHI, 0],
  [1, -PHI, 0],
  [-1, PHI, 0],
  [-1, -PHI, 0],
  [PHI, 0, 1],
  [PHI, 0, -1],
  [-PHI, 0, 1],
  [-PHI, 0, -1],
];

/** Unit-sphere icosahedron vertices. */
export const VERTICES: readonly Vec3[] = RAW_VERTICES.map(([x, y, z]) => [x / NORM, y / NORM, z / NORM]);

const EDGE_LENGTH = 2 / NORM;

/** Vertex index pairs of the 30 icosahedron edges. */
export const EDGES: readonly (readonly [number, number])[] = VERTICES.flatMap((a, i) =>
  VERTICES.flatMap((b, j) =>
    j > i && Math.abs(Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]) - EDGE_LENGTH) < 1e-6 ? [[i, j] as const] : [],
  ),
);

const NEIGHBOURS: readonly number[][] = VERTICES.map((_, i) =>
  EDGES.flatMap(([a, b]) => (a === i ? [b] : b === i ? [a] : [])),
);

export const PULSE_COUNT = 4;
const MIN_ROUTE = 14;
const MAX_ROUTE = 64;

const INNER_SCALE = 0.42;
const PERSPECTIVE = 0.16;
const OUTER_YAW_SPEED = 0.11;
const INNER_YAW_SPEED = -0.19;
const HOP_SECONDS = 1.5;
/** Trail length as a fraction of an edge. */
const TAIL = 0.45;

/**
 * Closed walk over the shell's edges that never doubles straight back. Seeded, so every render and test
 * sees the same routes.
 */
export function buildRoute(start: number, seed: number): number[] {
  let state = seed;
  const next = () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    // The low bits of an LCG cycle quickly, so choices come from the high ones.
    return state >>> 16;
  };
  const route = [start];
  let previous = -1;
  let current = start;
  while (route.length < MAX_ROUTE) {
    const closes = route.length >= MIN_ROUTE && previous !== start && NEIGHBOURS[current].includes(start);
    if (closes) {
      break;
    }
    const options = NEIGHBOURS[current].filter((n) => n !== previous);
    const chosen = options[next() % options.length];
    route.push(chosen);
    previous = current;
    current = chosen;
  }
  return route;
}

const ROUTES = Array.from({ length: PULSE_COUNT }, (_, i) => buildRoute((i * 5) % VERTICES.length, 7919 * (i + 1)));

function rotate([x, y, z]: Vec3, yaw: number, pitch: number): Vec3 {
  const cosY = Math.cos(yaw);
  const sinY = Math.sin(yaw);
  const x1 = x * cosY + z * sinY;
  const z1 = z * cosY - x * sinY;
  const cosP = Math.cos(pitch);
  const sinP = Math.sin(pitch);
  return [x1, y * cosP - z1 * sinP, y * sinP + z1 * cosP];
}

const lerp = (a: Vec3, b: Vec3, t: number): Vec3 => [
  a[0] + (b[0] - a[0]) * t,
  a[1] + (b[1] - a[1]) * t,
  a[2] + (b[2] - a[2]) * t,
];

const round = (value: number) => Math.round(value * 100) / 100;

/** Nearness of a point on the unit sphere: 0 at the back, 1 at the front. */
const nearness = (z: number) => (z + 1) / 2;

/** Lattice state at `time` seconds, in user units around (cx, cy). */
export function latticeFrame(time: number, cx: number, cy: number, radius: number): LatticeFrame {
  const project = ([x, y, z]: Vec3) => {
    const scale = radius / (1 - z * PERSPECTIVE);
    return { x: round(cx + x * scale), y: round(cy + y * scale) };
  };

  const segment = (a: Vec3, b: Vec3, opacity: number): LatticeSegment => {
    const from = project(a);
    const to = project(b);
    return { x1: from.x, y1: from.y, x2: to.x, y2: to.y, opacity: round(opacity) };
  };

  const outerPitch = 0.46 + Math.sin(time * 0.07) * 0.14;
  const outerPoints = VERTICES.map((v) => rotate(v, time * OUTER_YAW_SPEED, outerPitch));
  const innerPoints = VERTICES.map((v) => {
    const [x, y, z] = rotate(v, time * INNER_YAW_SPEED, -0.7);
    return [x * INNER_SCALE, y * INNER_SCALE, z * INNER_SCALE] as const;
  });

  const shell = (points: readonly Vec3[], reach: number, min: number, max: number) =>
    EDGES.map(([a, b]) => {
      const depth = nearness((points[a][2] + points[b][2]) / 2 / reach);
      return segment(points[a], points[b], min + (max - min) * depth);
    });

  // A vertex flares as a pulse arrives at it and fades as the pulse leaves.
  const flare = new Array<number>(VERTICES.length).fill(0);
  const pulses = ROUTES.map((route, i) => {
    const hops = time / HOP_SECONDS + i * 0.37;
    const index = Math.floor(hops) % route.length;
    const progress = hops - Math.floor(hops);
    const from = route[index];
    const to = route[(index + 1) % route.length];
    flare[to] = Math.max(flare[to], progress ** 3);
    flare[from] = Math.max(flare[from], (1 - progress) ** 3);
    const head = lerp(outerPoints[from], outerPoints[to], progress);
    const tail = lerp(outerPoints[from], outerPoints[to], Math.max(0, progress - TAIL));
    return segment(tail, head, 0.35 + 0.65 * nearness(head[2]));
  });

  const vertices = outerPoints.map((point, i): LatticeDot => {
    const depth = nearness(point[2]);
    return {
      ...project(point),
      r: round(1.4 + 1.8 * depth + 2.2 * flare[i]),
      opacity: round(Math.min(1, 0.3 + 0.6 * depth + 0.5 * flare[i])),
    };
  });

  return {
    outer: shell(outerPoints, 1, 0.14, 0.9),
    inner: shell(innerPoints, INNER_SCALE, 0.2, 0.75),
    vertices,
    pulses,
  };
}
