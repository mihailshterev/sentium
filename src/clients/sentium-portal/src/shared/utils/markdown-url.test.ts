import { describe, expect, it } from "vitest";
import { markdownUrlTransform } from "./markdown-url";

describe("markdownUrlTransform", () => {
  it("keeps obsidian:// links so assistants can cite vault notes", () => {
    expect(markdownUrlTransform("obsidian://open?vault=V&file=Note")).toBe("obsidian://open?vault=V&file=Note");
  });

  it("still strips dangerous protocols", () => {
    expect(markdownUrlTransform("javascript:alert(1)")).toBe("");
  });

  it("keeps ordinary web links", () => {
    expect(markdownUrlTransform("https://example.com")).toBe("https://example.com");
  });
});
