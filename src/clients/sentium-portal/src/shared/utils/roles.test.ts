import { describe, it, expect } from "vitest";
import { outranks } from "./roles";

describe("outranks", () => {
  it("Sovereign outranks Member", () => {
    expect(outranks("Sovereign", "Member")).toBe(true);
  });

  it("Member does not outrank Sovereign", () => {
    expect(outranks("Member", "Sovereign")).toBe(false);
  });

  it("an unlisted role does not outrank Member", () => {
    expect(outranks("UnknownRole", "Member")).toBe(false);
  });

  it("same role does not outrank itself", () => {
    expect(outranks("Member", "Member")).toBe(false);
  });
});
