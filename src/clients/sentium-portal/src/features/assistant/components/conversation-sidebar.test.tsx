import { describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ConversationSidebar } from "./conversation-sidebar";
import { formatConversationTime } from "@/features/assistant/utils/format-conversation-time";
import type { ConversationSummary } from "@/features/assistant/types/assistant";

const NOW = new Date(2026, 10, 7, 18, 0);

describe("formatConversationTime", () => {
  it("shows only the time for today and yesterday", () => {
    expect(formatConversationTime(new Date(2026, 10, 7, 9, 5).toISOString(), NOW)).toBe("09:05");
    expect(formatConversationTime(new Date(2026, 10, 6, 23, 59).toISOString(), NOW)).toBe("23:59");
  });

  it("adds the day for older dates this year", () => {
    expect(formatConversationTime(new Date(2026, 10, 2, 14, 23).toISOString(), NOW)).toBe("2 Nov, 14:23");
  });

  it("shows the year instead of the time for previous years", () => {
    expect(formatConversationTime(new Date(2025, 0, 15, 8, 0).toISOString(), NOW)).toBe("15 Jan 2025");
  });

  it("returns nothing for an invalid date", () => {
    expect(formatConversationTime("not a date", NOW)).toBe("");
  });
});

const conversation: ConversationSummary = {
  id: "c1",
  title: "Trip planning",
  model: "qwen3-4b",
  createdAt: new Date().toISOString(),
};

const renderSidebar = (overrides: Partial<React.ComponentProps<typeof ConversationSidebar>> = {}) => {
  const props: React.ComponentProps<typeof ConversationSidebar> = {
    isOpen: true,
    conversations: [conversation],
    conversationGroups: [{ label: "Today", items: [conversation] }],
    totalCount: 1,
    search: "",
    onSearchChange: vi.fn(),
    activeConversationId: "c1",
    isCreating: false,
    hasMoreConversations: false,
    isLoadingMoreConversations: false,
    onLoadMoreConversations: vi.fn(),
    wsContextOpen: false,
    workspaces: [],
    expandedWorkspace: null,
    expandedWorkspaceFiles: [],
    onNewConversation: vi.fn(),
    onLoadConversation: vi.fn(),
    onDeleteConversation: vi.fn(),
    onRenameConversation: vi.fn(),
    onToggleWsContext: vi.fn(),
    onToggleExpandWorkspace: vi.fn(),
    onInjectWorkspaceContext: vi.fn(),
    onInjectFileContext: vi.fn(),
    ...overrides,
  };
  render(<ConversationSidebar {...props} />);
  return props;
};

describe("ConversationSidebar", () => {
  it("shows the title with its start time as a separate detail", () => {
    renderSidebar();
    expect(screen.getByText("Trip planning")).toBeInTheDocument();
    const time = document.querySelector("time");
    expect(time).toHaveAttribute("dateTime", conversation.createdAt);
    expect(time?.textContent).toMatch(/^\d{2}:\d{2}$/);
  });

  it("marks the active conversation", () => {
    renderSidebar();
    expect(screen.getByRole("button", { name: /^Trip planning/ })).toHaveAttribute("aria-current", "true");
  });

  it("reports search input and clears it on Escape", () => {
    const props = renderSidebar({ search: "tri" });
    const input = screen.getByLabelText("Search conversations");

    fireEvent.change(input, { target: { value: "trip" } });
    expect(props.onSearchChange).toHaveBeenCalledWith("trip");

    fireEvent.keyDown(input, { key: "Escape" });
    expect(props.onSearchChange).toHaveBeenCalledWith("");
  });

  it("says when a search has no matches", () => {
    renderSidebar({ search: "zebra", conversations: [], conversationGroups: [], totalCount: 0 });
    expect(screen.getByText("No conversations match “zebra”")).toBeInTheDocument();
  });

  it("shows the matching count in the header", () => {
    renderSidebar({ totalCount: 42 });
    expect(screen.getByText("42")).toBeInTheDocument();
  });
});
