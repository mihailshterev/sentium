import { describe, it, expect } from "vitest";
import { parseColor } from "./parse-color";

describe("parseColor", () => {
  it("parses #rrggbb", () => {
    expect(parseColor("#ff8000")).toEqual([1, 128 / 255, 0]);
  });

  it("parses #rgb shorthand", () => {
    expect(parseColor("#fff")).toEqual([1, 1, 1]);
  });

  it("parses rgb() and rgba() with surrounding whitespace", () => {
    expect(parseColor("  rgb(255, 0, 51) ")).toEqual([1, 0, 51 / 255]);
    expect(parseColor("rgba(0 255 0 / 0.5)")).toEqual([0, 1, 0]);
  });

  it("returns null for anything else", () => {
    expect(parseColor("")).toBeNull();
    expect(parseColor("navy")).toBeNull();
    expect(parseColor("#12345")).toBeNull();
  });
});
