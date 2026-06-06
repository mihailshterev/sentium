import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import SettingsPage from "./settings";
import * as useSystemSettingsHook from "../../hooks/useSystemSettings";
import type { Settings } from "../../types/agentConfig";

const mockSettings: Settings = {
  harness: {
    UserHarnessPrompt: "Be concise.",
    IsBuiltInHarnessEnabled: true,
    IsPromptEnhancementEnabled: true,
  },
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: "alice@example.com",
};

const defaultHook = {
  settings: mockSettings,
  isLoading: false,
  error: null,
  save: vi.fn(),
  isSaving: false,
  isSaveSuccess: false,
  isSaveError: false,
  saveError: null,
  resetSave: vi.fn(),
};

const renderSettings = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue(defaultHook);
});

describe("Settings loading state", () => {
  it("renders 'Loading settings…' while loading", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: undefined,
      isLoading: true,
    });
    renderSettings();
    expect(screen.getByText(/loading settings/i)).toBeInTheDocument();
  });

  it("renders loading state when settings is null/undefined (not yet fetched)", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: undefined,
      isLoading: false,
    });
    renderSettings();
    expect(screen.getByText(/loading settings/i)).toBeInTheDocument();
  });
});

describe("Settings loaded state", () => {
  it("renders the page title", () => {
    renderSettings();
    expect(screen.getAllByText("Settings").length).toBeGreaterThan(0);
  });

  it("renders the prompt textarea pre-filled with initial content", () => {
    renderSettings();
    const textarea = screen.getByRole("textbox");
    expect((textarea as HTMLTextAreaElement).value).toBe("Be concise.");
  });

  it("renders the 'Last saved by' pill when updatedBy is set", () => {
    renderSettings();
    expect(screen.getByText(/last saved by alice@example.com/i)).toBeInTheDocument();
  });

  it("does not render 'Last saved by' when updatedBy is null", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: { ...mockSettings, updatedBy: null },
    });
    renderSettings();
    expect(screen.queryByText(/last saved by/i)).not.toBeInTheDocument();
  });

  it("renders the built-in harness toggle as checked by default", () => {
    renderSettings();
    const toggle = screen.getAllByRole("checkbox")[0];
    expect((toggle as HTMLInputElement).checked).toBe(true);
  });

  it("renders save button disabled when content is not dirty", () => {
    renderSettings();
    expect(screen.getByRole("button", { name: /save changes/i })).toBeDisabled();
  });
});

describe("Settings editing interactions", () => {
  it("enables save button when prompt is changed", () => {
    renderSettings();
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "New instruction." } });
    expect(screen.getByRole("button", { name: /save changes/i })).not.toBeDisabled();
  });

  it("enables save button when toggle is changed", () => {
    renderSettings();
    fireEvent.click(screen.getAllByRole("checkbox")[0]);
    expect(screen.getByRole("button", { name: /save changes/i })).not.toBeDisabled();
  });

  it("calls save with correct payload when Save Changes is clicked", async () => {
    const save = vi.fn();
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({ ...defaultHook, save });
    renderSettings();
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Updated prompt." } });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));
    await waitFor(() =>
      expect(save).toHaveBeenCalledWith({
        harness: {
          UserHarnessPrompt: "Updated prompt.",
          IsBuiltInHarnessEnabled: true,
          IsPromptEnhancementEnabled: true,
        },
      }),
    );
  });

  it("shows isSaving state on the save button", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      isSaving: true,
      settings: { ...mockSettings, harness: { ...mockSettings.harness, UserHarnessPrompt: "Changed" } },
    });
    renderSettings();
    expect(screen.getByRole("button", { name: /saving/i })).toBeDisabled();
  });

  it("shows success alert when isSaveSuccess is true", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      isSaveSuccess: true,
    });
    renderSettings();
    expect(screen.getByText(/system parameters/i)).toBeInTheDocument();
  });

  it("shows error alert when isSaveError is true", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      isSaveError: true,
      saveError: new Error("Save failed due to conflict"),
    });
    renderSettings();
    expect(screen.getByText(/save failed due to conflict/i)).toBeInTheDocument();
  });

  it("shows fallback error message when saveError has no message", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      isSaveError: true,
      saveError: null,
    });
    renderSettings();
    expect(screen.getByText(/failed to save configuration/i)).toBeInTheDocument();
  });

  it("shows characters-over-limit warning when text exceeds 16000 chars", () => {
    const longPrompt = "x".repeat(16001);
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: { ...mockSettings, harness: { ...mockSettings.harness, UserHarnessPrompt: longPrompt } },
    });
    renderSettings();
    expect(screen.getByText(/characters over limit/i)).toBeInTheDocument();
  });

  it("disables save when content is over the character limit", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: { ...mockSettings, harness: { ...mockSettings.harness, UserHarnessPrompt: "x" } },
    });
    renderSettings();
    const textarea = screen.getByRole("textbox");
    fireEvent.change(textarea, { target: { value: "x".repeat(16001) } });
    expect(screen.getByRole("button", { name: /save changes/i })).toBeDisabled();
  });

  it("shows near-limit warning when close to character limit", () => {
    vi.spyOn(useSystemSettingsHook, "useSystemSettings").mockReturnValue({
      ...defaultHook,
      settings: { ...mockSettings, harness: { ...mockSettings.harness, UserHarnessPrompt: "x".repeat(15600) } },
    });
    renderSettings();
    // Should still show characters remaining in the warn style (near limit)
    expect(screen.getByText(/characters remaining/i)).toBeInTheDocument();
  });
});
