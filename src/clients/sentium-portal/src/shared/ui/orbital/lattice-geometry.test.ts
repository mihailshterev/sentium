import { describe, expect, it } from "vitest";
import { EDGES, PULSE_COUNT, VERTICES, buildRoute, latticeFrame } from "./lattice-geometry";

const isEdge = (a: number, b: number) => EDGES.some(([x, y]) => (x === a && y === b) || (x === b && y === a));

describe("lattice geometry", () => {
  it("describes an icosahedron on the unit sphere", () => {
    expect(VERTICES).toHaveLength(12);
    expect(EDGES).toHaveLength(30);
    VERTICES.forEach((v) => expect(Math.hypot(...v)).toBeCloseTo(1));
  });

  it("builds closed routes that follow edges and never double straight back", () => {
    const route = buildRoute(0, 42);
    expect(route.length).toBeGreaterThanOrEqual(14);
    route.forEach((vertex, i) => {
      const next = route[(i + 1) % route.length];
      expect(isEdge(vertex, next)).toBe(true);
      if (i >= 2) {
        expect(vertex).not.toBe(route[i - 2]);
      }
    });
  });

  it("is deterministic for a given seed", () => {
    expect(buildRoute(3, 7)).toEqual(buildRoute(3, 7));
  });

  it("produces a frame that stays inside the perspective-enlarged radius", () => {
    const frame = latticeFrame(12.3, 400, 320, 100);
    expect(frame.outer).toHaveLength(30);
    expect(frame.inner).toHaveLength(30);
    expect(frame.vertices).toHaveLength(12);
    expect(frame.pulses).toHaveLength(PULSE_COUNT);
    frame.vertices.forEach((v) => {
      expect(Math.hypot(v.x - 400, v.y - 320)).toBeLessThan(120);
      expect(v.opacity).toBeGreaterThan(0);
      expect(v.opacity).toBeLessThanOrEqual(1);
    });
  });

  it("moves between frames", () => {
    expect(latticeFrame(1, 0, 0, 100).outer).not.toEqual(latticeFrame(2, 0, 0, 100).outer);
  });
});
