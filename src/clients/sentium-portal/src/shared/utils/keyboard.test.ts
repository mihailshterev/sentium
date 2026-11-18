import { describe, expect, it } from "vitest";
import { isImeComposing } from "./keyboard";

describe("isImeComposing", () => {
  it("is false for a plain Enter", () => {
    expect(isImeComposing(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13 }))).toBe(false);
  });

  it("is true while composing", () => {
    expect(isImeComposing(new KeyboardEvent("keydown", { key: "Enter", isComposing: true }))).toBe(true);
  });

  it("is true for Safari's out-of-order confirming Enter (keyCode 229)", () => {
    expect(isImeComposing(new KeyboardEvent("keydown", { key: "Enter", keyCode: 229 }))).toBe(true);
  });

  it("reads the native event from a React synthetic event", () => {
    const nativeEvent = new KeyboardEvent("keydown", { key: "Enter", isComposing: true });
    expect(isImeComposing({ nativeEvent })).toBe(true);
  });
});
