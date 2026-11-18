import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import * as service from "@/features/vault/api/vault.service";
import type { VaultNote, VaultSummary } from "@/features/vault/types/vault";
import { VaultView } from "./vault-view";

vi.mock("@/features/vault/api/vault.service");

const summary: VaultSummary = {
  id: "v1",
  name: "Second Brain",
  deviceName: "DESKTOP",
  noteCount: 2,
  linkCount: 1,
  unresolvedLinkCount: 1,
  orphanCount: 0,
  pendingIndexCount: 0,
  failedIndexCount: 0,
  lastSyncAt: new Date().toISOString(),
};

const ideas: VaultNote = {
  id: "n2",
  path: "Ideas.md",
  title: "Ideas",
  obsidianUri: "obsidian://open?vault=Second%20Brain&file=Ideas",
  content: "Back to [[Hub]] and [[Missing note]].",
  tags: ["thinking"],
  aliases: [],
  frontmatter: null,
  wordCount: 5,
  updatedAt: "2026-11-18T10:00:00Z",
  indexedAt: null,
  links: [
    { text: "Hub", heading: null, isEmbed: false, target: { id: "n1", path: "Hub.md", title: "Hub", obsidianUri: "obsidian://hub", tags: [] } },
    { text: "Missing note", heading: null, isEmbed: false, target: null },
  ],
  backlinks: [{ id: "n1", path: "Hub.md", title: "Hub", obsidianUri: "obsidian://hub", tags: [] }],
};

function renderView(path = "/vault") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <VaultView />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("VaultView", () => {
  beforeEach(() => {
    vi.mocked(service.fetchVaultTags).mockResolvedValue([{ tag: "thinking", count: 1 }]);
    vi.mocked(service.fetchVaultNotes).mockResolvedValue({
      items: [{ id: "n2", path: "Ideas.md", title: "Ideas", tags: ["thinking"], wordCount: 5, updatedAt: ideas.updatedAt, indexed: false }],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    });
    vi.mocked(service.fetchVaultNote).mockResolvedValue(ideas);
  });

  it("explains how to sync a vault when none exists", async () => {
    vi.mocked(service.fetchVaultSummary).mockResolvedValue(null);

    renderView();

    expect(await screen.findByText("No vault synced yet")).toBeInTheDocument();
  });

  it("lists notes and renders the selected one with followable wikilinks and backlinks", async () => {
    vi.mocked(service.fetchVaultSummary).mockResolvedValue(summary);

    renderView();

    fireEvent.click(await screen.findByRole("button", { name: /Ideas/ }));

    const panel = await screen.findByTestId("vault-note-panel");
    expect(panel).toHaveTextContent("Open in Obsidian");
    expect(screen.getByRole("link", { name: /Open in Obsidian/ })).toHaveAttribute("href", ideas.obsidianUri);
    expect(within(screen.getByRole("list", { name: "Notes" })).getByText("indexing")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Hub" })).toHaveLength(2);
    expect(screen.getAllByText("Missing note").length).toBeGreaterThan(0);
  });
});
