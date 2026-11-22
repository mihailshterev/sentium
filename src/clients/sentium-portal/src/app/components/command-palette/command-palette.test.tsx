import { describe, it, expect, vi, beforeEach } from "vitest";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router";
import { CommandPalette } from "./command-palette";
import { openCommandPalette } from "./open-command-palette";
import * as useConversationsHook from "@/features/assistant/hooks/useConversations";
import { useAuthStore } from "@/shared/auth/state/auth-store";
import { AUTH_STATUS } from "@/shared/utils/constants";

function Location() {
  return <output data-testid="location">{useLocation().pathname}</output>;
}

const renderPalette = () =>
  render(
    <MemoryRouter>
      <CommandPalette />
      <Location />
    </MemoryRouter>,
  );

beforeEach(() => {
  useAuthStore.setState({
    user: { sub: "u1", name: "User", roles: ["Member"] },
    status: AUTH_STATUS.AUTHENTICATED,
  });
  vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
    conversations: [{ id: "c1", title: "Trip plan", model: "gemma", createdAt: new Date().toISOString() }],
    isLoading: false,
  } as unknown as ReturnType<typeof useConversationsHook.useConversations>);
});

describe("CommandPalette", () => {
  it("opens with Ctrl+K and focuses the search", async () => {
    renderPalette();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await userEvent.keyboard("{Control>}k{/Control}");
    expect(screen.getByRole("dialog", { name: "Command palette" })).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toHaveFocus();
  });

  it("leaves Ctrl+K to a field that already handled it", () => {
    renderPalette();
    const event = new KeyboardEvent("keydown", { key: "k", ctrlKey: true, cancelable: true });
    event.preventDefault();
    act(() => {
      window.dispatchEvent(event);
    });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("opens from a button anywhere", () => {
    renderPalette();
    act(() => openCommandPalette());
    expect(screen.getByRole("dialog", { name: "Command palette" })).toBeInTheDocument();
  });

  it("finds pages by what they are for, not only by name", async () => {
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.type(screen.getByRole("combobox"), "monitoring");
    expect(screen.getByRole("option", { name: /vigil/i })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /^agents/i })).not.toBeInTheDocument();
  });

  it("hides Sovereign-only pages from members", async () => {
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.type(screen.getByRole("combobox"), "users");
    expect(screen.queryByRole("option", { name: /^users/i })).not.toBeInTheDocument();
  });

  it("navigates to the highlighted result with the keyboard", async () => {
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.type(screen.getByRole("combobox"), "workflows");
    const options = screen.getAllByRole("option");
    expect(options[0]).toHaveAttribute("aria-selected", "true");
    await userEvent.keyboard("{ArrowDown}");
    expect(options[1]).toHaveAttribute("aria-selected", "true");
    await userEvent.keyboard("{ArrowUp}{Enter}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByTestId("location")).toHaveTextContent("/workflows");
  });

  it("reopens a conversation", async () => {
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.click(screen.getByRole("option", { name: /trip plan/i }));
    expect(screen.getByTestId("location")).toHaveTextContent("/assistant/c1");
  });

  it("closes on Escape", async () => {
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("says when nothing matches", async () => {
    vi.spyOn(useConversationsHook, "useConversations").mockReturnValue({
      conversations: [],
      isLoading: false,
    } as unknown as ReturnType<typeof useConversationsHook.useConversations>);
    renderPalette();
    act(() => openCommandPalette());
    await userEvent.type(screen.getByRole("combobox"), "zzzz");
    expect(screen.getByText(/no matches/i)).toBeInTheDocument();
  });
});
