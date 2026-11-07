import { describe, expect, it } from "vitest";
import {
  CX,
  CY,
  angleDistance,
  labelSide,
  nearestByAngle,
  placeRing,
  polar,
  toPercent,
  toSlug,
} from "./orbital-geometry";

describe("polar", () => {
  it("measures angles clockwise from east around the centre", () => {
    expect(polar(100, 0)).toEqual({ x: CX + 100, y: CY });
    const south = polar(100, 90);
    expect(south.x).toBeCloseTo(CX);
    expect(south.y).toBeCloseTo(CY + 100);
  });
});

describe("labelSide", () => {
  it.each([
    [0, "right"],
    [180, "left"],
    [-90, "top"],
    [90, "bottom"],
    [-140, "left"],
  ])("puts the label of a node at %i° on the %s", (deg, side) => {
    expect(labelSide(deg)).toBe(side);
  });
});

describe("placeRing", () => {
  it("spreads nodes evenly from the start angle", () => {
    const placed = placeRing(["a", "b", "c", "d"], 100, -90);
    expect(placed.map((p) => p.deg)).toEqual([-90, 0, 90, 180]);
    expect(placed[1].labelPoint.x).toBeCloseTo(CX + 114);
  });

  it("returns nothing for an empty ring", () => {
    expect(placeRing([], 100, 0)).toEqual([]);
  });
});

describe("angles", () => {
  it("measures the short way round", () => {
    expect(angleDistance(350, 10)).toBe(20);
    expect(angleDistance(-170, 170)).toBe(20);
  });

  it("finds the nearest placed node, or -1", () => {
    expect(nearestByAngle([{ deg: 0 }, { deg: 120 }, { deg: 240 }], 200)).toBe(2);
    expect(nearestByAngle([], 0)).toBe(-1);
  });
});

describe("formatting", () => {
  it("converts view-box points to percentages", () => {
    expect(toPercent({ x: CX, y: CY })).toEqual({ left: "50%", top: "50%" });
  });

  it("slugs and truncates labels", () => {
    expect(toSlug("  Sentium.Web ")).toBe("sentium-web");
    expect(toSlug("A very long service name", 10)).toBe("a-very-lo…");
  });
});
