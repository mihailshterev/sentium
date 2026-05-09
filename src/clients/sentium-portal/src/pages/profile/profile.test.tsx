import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Profile from "./profile";
import { useAuthStore } from "../../stores/auth-store";
import * as useProfileHook from "../../hooks/useProfile";
import * as useRoleHook from "../../hooks/useRole";
import type { UserProfile } from "../../services/identity.service";

vi.mock("../login/animated-bg", () => ({
  AnimatedBg: () => <canvas data-testid="animated-bg" />,
}));

const mockProfile: UserProfile = {
  id: "u1",
  email: "alice@example.com",
  firstName: "Alice",
  lastName: "Smith",
};

const defaultProfileHook = {
  profile: mockProfile,
  isLoading: false,
  updateProfile: vi.fn().mockResolvedValue(undefined),
  isSaving: false,
  saveError: null,
  isSaveSuccess: false,
  resetSave: vi.fn(),
};

const defaultRoleHook = {
  roles: ["Member"],
  highestRole: "Member" as const,
  isSovereign: false,
  isMemberOrAbove: true,
  isAuthenticated: true,
  hasRole: vi.fn(() => false),
};

const renderProfile = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Profile />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  useAuthStore.setState({
    user: { sub: "u1", email: "alice@example.com", name: "Alice", roles: ["Member"] },
    status: "authenticated",
  });
  vi.spyOn(useProfileHook, "default").mockReturnValue(defaultProfileHook);
  vi.spyOn(useRoleHook, "useRole").mockReturnValue(defaultRoleHook);
});

describe("Profile – loading state", () => {
  it("renders a skeleton when loading", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, isLoading: true, profile: null });
    renderProfile();
    expect(screen.queryByLabelText(/first name/i)).not.toBeInTheDocument();
  });
});

describe("Profile loaded state", () => {
  it("renders the user's display name", () => {
    renderProfile();
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("renders the user's email in the avatar section", () => {
    renderProfile();
    expect(screen.getAllByText("alice@example.com").length).toBeGreaterThan(0);
  });

  it("renders the role badge", () => {
    renderProfile();
    expect(screen.getByText("Member")).toBeInTheDocument();
  });

  it("renders the edit form fields pre-filled", () => {
    renderProfile();
    expect((screen.getByLabelText(/first name/i) as HTMLInputElement).value).toBe("Alice");
    expect((screen.getByLabelText(/last name/i) as HTMLInputElement).value).toBe("Smith");
    expect((screen.getByLabelText(/email address/i) as HTMLInputElement).value).toBe("alice@example.com");
  });

  it("handles a profile with no lastName (shows email as display name)", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({
      ...defaultProfileHook,
      profile: { ...mockProfile, firstName: "", lastName: null },
    });
    renderProfile();
    expect(screen.getAllByText("alice@example.com").length).toBeGreaterThan(0);
  });

  it("renders without role badge when no roles", () => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue({ ...defaultRoleHook, highestRole: undefined });
    renderProfile();
    expect(screen.queryByText("Member")).not.toBeInTheDocument();
  });

  it("does not render the edit form when profile is null", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, profile: null });
    renderProfile();
    expect(screen.queryByLabelText(/first name/i)).not.toBeInTheDocument();
  });
});

describe("Profile form interactions", () => {
  it("calls updateProfile with trimmed values on save", async () => {
    const updateProfile = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, updateProfile });
    renderProfile();
    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: "  Bob  " } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(updateProfile).toHaveBeenCalledWith(expect.objectContaining({ firstName: "Bob" })));
  });

  it("shows isSaving state on the save button", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, isSaving: true });
    renderProfile();
    expect(screen.getByRole("button", { name: /saving/i })).toBeDisabled();
  });

  it("shows save error banner when saveError is set", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({
      ...defaultProfileHook,
      saveError: new Error("Server rejected the update"),
    });
    renderProfile();
    expect(screen.getByText(/server rejected the update/i)).toBeInTheDocument();
  });

  it("shows success banner when isSaveSuccess is true", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, isSaveSuccess: true });
    renderProfile();
    expect(screen.getByText(/profile updated successfully/i)).toBeInTheDocument();
  });

  it("trims lastName to null when left empty", async () => {
    const updateProfile = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useProfileHook, "default").mockReturnValue({ ...defaultProfileHook, updateProfile });
    renderProfile();
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: "" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(updateProfile).toHaveBeenCalledWith(expect.objectContaining({ lastName: null })));
  });
});
