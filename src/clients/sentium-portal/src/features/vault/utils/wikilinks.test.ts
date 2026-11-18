import { describe, expect, it } from "vitest";
import type { VaultLink } from "@/features/vault/types/vault";
import { resolveWikilink, wikilinksToMarkdown } from "./wikilinks";

describe("wikilinksToMarkdown", () => {
  it("turns wikilinks into followable links labelled as Obsidian shows them", () => {
    expect(wikilinksToMarkdown("See [[Folder/Note B#Intro]] and [[Note A|the alias]] and ![[Embed]]")).toBe(
      "See [Note B › Intro](vault-note:Folder%2FNote%20B) and [the alias](vault-note:Note%20A) and [Embed](vault-note:Embed)",
    );
  });

  it("leaves fenced code alone", () => {
    const content = "```\n[[Not a link]]\n```\n[[Link]]";
    expect(wikilinksToMarkdown(content)).toBe("```\n[[Not a link]]\n```\n[Link](vault-note:Link)");
  });

  it("escapes backslashes in the label so they cannot break the link", () => {
    expect(wikilinksToMarkdown("[[Note|ends in \\]]")).toBe("[ends in \\\\](vault-note:Note)");
  });

  it("keeps a heading-only link as plain text", () => {
    expect(wikilinksToMarkdown("Jump to [[#Section]]")).toBe("Jump to Section");
  });
});

describe("resolveWikilink", () => {
  const target = { id: "n1", path: "Folder/Note B.md", title: "Note B", obsidianUri: "obsidian://x", tags: [] };
  const links: VaultLink[] = [
    { text: "Folder/Note B", heading: "Intro", isEmbed: false, target },
    { text: "Missing", heading: null, isEmbed: false, target: null },
  ];

  it("finds the server's resolution of the link, case-insensitively", () => {
    expect(resolveWikilink("vault-note:folder%2Fnote%20b", links)?.target).toBe(target);
  });

  it("returns the unresolved link so the renderer can mark it", () => {
    expect(resolveWikilink("vault-note:Missing", links)?.target).toBeNull();
  });
});
