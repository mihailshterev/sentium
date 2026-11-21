import { describe, it, expect, afterEach } from "vitest";
import { readMapColors } from "./map-colors";

const root = document.documentElement;

afterEach(() => {
  root.style.removeProperty("--bg-base");
  root.style.removeProperty("--text-primary");
});

describe("readMapColors", () => {
  it("takes the background from the palette's base token", () => {
    root.style.setProperty("--bg-base", "#102030");
    expect(readMapColors(false).background).toBe(0x102030);
  });

  it("places the grid between the background and the text colour", () => {
    root.style.setProperty("--bg-base", "#000000");
    root.style.setProperty("--text-primary", "#ffffff");
    const { background, gridLine, gridDot, edge } = readMapColors(false);

    expect(background).toBeLessThan(gridLine);
    expect(gridLine).toBeLessThan(gridDot);
    expect(gridDot).toBeLessThan(edge);
  });

  it("falls back to the default palette when the tokens are missing", () => {
    expect(readMapColors(false).background).toBe(0x0a0806);
  });
});
