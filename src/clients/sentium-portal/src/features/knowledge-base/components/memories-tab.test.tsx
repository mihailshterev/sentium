import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import * as memoriesService from "../api/memories.service";
import type { UserMemory } from "../types/user-memory";
import { MemoriesTab } from "./memories-tab";

const memory: UserMemory = {
  id: "m1",
  content: "I am vegetarian.",
  createdAt: "2026-01-02T10:00:00Z",
  updatedAt: "2026-01-02T10:00:00Z",
};

const renderTab = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoriesTab />
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("MemoriesTab", () => {
  it("lists the user's memories", async () => {
    vi.spyOn(memoriesService, "fetchMemories").mockResolvedValue([memory]);
    renderTab();
    expect(await screen.findByText("I am vegetarian.")).toBeInTheDocument();
    expect(screen.getByText("1 saved")).toBeInTheDocument();
  });

  it("explains how memories appear when there are none", async () => {
    vi.spyOn(memoriesService, "fetchMemories").mockResolvedValue([]);
    renderTab();
    expect(await screen.findByText("Nothing remembered yet")).toBeInTheDocument();
  });

  it("adds a memory", async () => {
    vi.spyOn(memoriesService, "fetchMemories").mockResolvedValue([]);
    const add = vi.spyOn(memoriesService, "addMemory").mockResolvedValue(memory);
    renderTab();
    fireEvent.change(screen.getByLabelText("New memory"), { target: { value: "  I am vegetarian.  " } });
    fireEvent.click(screen.getByRole("button", { name: /remember/i }));
    await waitFor(() => expect(add).toHaveBeenCalledWith("I am vegetarian."));
  });

  it("corrects a memory", async () => {
    vi.spyOn(memoriesService, "fetchMemories").mockResolvedValue([memory]);
    const update = vi.spyOn(memoriesService, "updateMemory").mockResolvedValue({ ...memory, content: "I am vegan." });
    renderTab();
    fireEvent.click(await screen.findByTitle("Edit memory"));
    fireEvent.change(screen.getByLabelText("What agents should remember"), { target: { value: "I am vegan." } });
    fireEvent.click(screen.getByRole("button", { name: /^save$/i }));
    await waitFor(() => expect(update).toHaveBeenCalledWith("m1", "I am vegan."));
  });

  it("forgets a memory after confirmation", async () => {
    vi.spyOn(memoriesService, "fetchMemories").mockResolvedValue([memory]);
    const remove = vi.spyOn(memoriesService, "deleteMemory").mockResolvedValue(undefined);
    renderTab();
    fireEvent.click(await screen.findByTitle("Forget memory"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: /^forget$/i }));
    await waitFor(() => expect(remove).toHaveBeenCalledWith("m1"));
  });
});
