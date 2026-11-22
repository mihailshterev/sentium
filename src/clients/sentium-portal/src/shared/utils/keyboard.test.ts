import { describe, expect, it } from "vitest";
import { isEditableTarget, isImeComposing } from "./keyboard";

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

describe("isEditableTarget", () => {
  it("is true for text fields", () => {
    expect(isEditableTarget(document.createElement("input"))).toBe(true);
    expect(isEditableTarget(document.createElement("textarea"))).toBe(true);
  });

  it("is false for other elements and non-elements", () => {
    expect(isEditableTarget(document.createElement("button"))).toBe(false);
    expect(isEditableTarget(null)).toBe(false);
    expect(isEditableTarget(window)).toBe(false);
  });
});
