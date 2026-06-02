import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Users from "./users";
import { useAuthStore } from "../../stores/auth-store";
import * as useUsersHook from "../../hooks/useUsers";
import * as useRoleHook from "../../hooks/useRole";
import type { UserListItem } from "../../services/identity.service";

const mockUser: UserListItem = {
  id: "user-1",
  email: "alice@example.com",
  firstName: "Alice",
  lastName: "Smith",
  roles: ["Member"],
  isLockedOut: false,
};

const mockUserNoName: UserListItem = {
  id: "user-2",
  email: "bob@example.com",
  firstName: "",
  lastName: null,
  roles: [],
  isLockedOut: true,
};

const defaultUsersHook = {
  users: [mockUser],
  totalCount: 1,
  totalPages: 1,
  page: 1,
  pageSize: 20,
  setPage: vi.fn(),
  isLoading: false,
  isFetching: false,
  error: null,
  refetch: vi.fn(),
  assignRole: vi.fn().mockResolvedValue(undefined),
  isAssigningRole: false,
  removeRole: vi.fn().mockResolvedValue(undefined),
  isRemovingRole: false,
  deleteUser: vi.fn().mockResolvedValue(undefined),
  isDeletingUser: false,
};

const memberRole = {
  roles: ["Member"],
  highestRole: "Member" as const,
  isSovereign: false,
  isMemberOrAbove: true,
  isAuthenticated: true,
  hasRole: vi.fn(() => false),
};

const sovereignRole = {
  roles: ["Sovereign"],
  highestRole: "Sovereign" as const,
  isSovereign: true,
  isMemberOrAbove: true,
  isAuthenticated: true,
  hasRole: vi.fn(() => true),
};

const renderUsers = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Users />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  useAuthStore.setState({
    user: { sub: "current-user", email: "me@example.com", name: "Me", roles: ["Member"] },
    status: "authenticated",
  });
  vi.spyOn(useUsersHook, "default").mockReturnValue(defaultUsersHook);
  vi.spyOn(useRoleHook, "useRole").mockReturnValue(memberRole);
});

describe("Users loading state", () => {
  it("renders skeleton rows while loading", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, isLoading: true });
    renderUsers();
    expect(screen.queryByText("alice@example.com")).not.toBeInTheDocument();
  });
});

describe("Users error state", () => {
  it("shows error message when fetch fails", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      users: [],
      error: new Error("Failed to load"),
    });
    renderUsers();
    expect(screen.getByText(/failed to load users/i)).toBeInTheDocument();
    expect(screen.getAllByText(/failed to load/i).length).toBeGreaterThanOrEqual(1);
  });
});

describe("Users empty state", () => {
  it("shows 'No users found' when list is empty", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, users: [] });
    renderUsers();
    expect(screen.getByText(/no users found/i)).toBeInTheDocument();
  });
});

describe("Users list state", () => {
  it("renders user name from firstName and lastName", () => {
    renderUsers();
    expect(screen.getByText("Alice Smith")).toBeInTheDocument();
  });

  it("renders — when user has no name", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      users: [mockUserNoName],
    });
    renderUsers();
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(1);
  });

  it("renders user email", () => {
    renderUsers();
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
  });

  it("renders role badges for each user role", () => {
    renderUsers();
    expect(screen.getByText("Member")).toBeInTheDocument();
  });

  it("shows — for roles when user has no roles", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      users: [{ ...mockUser, roles: [] }],
    });
    renderUsers();
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("renders 'you' tag for the current logged-in user", () => {
    useAuthStore.setState({
      user: { sub: "user-1", email: "alice@example.com", name: "Alice", roles: ["Member"] },
      status: "authenticated",
    });
    renderUsers();
    expect(screen.getByText("you")).toBeInTheDocument();
  });

  it("renders 'locked' tag for locked-out users", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      users: [{ ...mockUser, isLockedOut: true }],
    });
    renderUsers();
    expect(screen.getByText("locked")).toBeInTheDocument();
  });

  it("shows user count in the header", () => {
    renderUsers();
    expect(screen.getByText("1 user")).toBeInTheDocument();
  });

  it("shows user count with plural", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      users: [mockUser, mockUserNoName],
      totalCount: 2,
    });
    renderUsers();
    expect(screen.getByText("2 users")).toBeInTheDocument();
  });
});

describe("Users sovereign controls", () => {
  beforeEach(() => {
    vi.spyOn(useRoleHook, "useRole").mockReturnValue(sovereignRole);
    vi.spyOn(useUsersHook, "default").mockReturnValue(defaultUsersHook);
  });

  it("renders role assignment select for sovereign", () => {
    renderUsers();
    expect(screen.getByRole("combobox")).toBeInTheDocument();
  });

  it("renders remove role buttons for sovereign", () => {
    renderUsers();
    expect(screen.getByTitle("Remove Member")).toBeInTheDocument();
  });

  it("calls assignRole when a role is selected from the dropdown", async () => {
    const assignRole = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, assignRole });
    renderUsers();
    const select = screen.getByRole("combobox");
    fireEvent.change(select, { target: { value: "Sovereign" } });
    await waitFor(() => expect(assignRole).toHaveBeenCalledWith({ userId: "user-1", roleName: "Sovereign" }));
  });

  it("calls removeRole when remove role button is clicked", async () => {
    const removeRole = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, removeRole });
    renderUsers();
    fireEvent.click(screen.getByTitle("Remove Member"));
    await waitFor(() => expect(removeRole).toHaveBeenCalledWith({ userId: "user-1", roleName: "Member" }));
  });

  it("shows generic error when removeRole throws a non-Error value", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      removeRole: vi.fn().mockRejectedValue("string error"),
    });
    renderUsers();
    fireEvent.click(screen.getByTitle("Remove Member"));
    await waitFor(() => expect(screen.getByText("Failed to remove role.")).toBeInTheDocument());
  });

  it("shows actionError when assignRole throws", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      assignRole: vi.fn().mockRejectedValue(new Error("Permission denied")),
    });
    renderUsers();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Sovereign" } });
    await waitFor(() => expect(screen.getByText("Permission denied")).toBeInTheDocument());
  });

  it("shows generic error when assignRole throws a non-Error value", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      assignRole: vi.fn().mockRejectedValue("plain error"),
    });
    renderUsers();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Sovereign" } });
    await waitFor(() => expect(screen.getByText("Failed to assign role.")).toBeInTheDocument());
  });

  it("dismisses actionError when × button is clicked", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      assignRole: vi.fn().mockRejectedValue(new Error("Permission denied")),
    });
    renderUsers();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Sovereign" } });
    await waitFor(() => screen.getByText("Permission denied"));
    fireEvent.click(screen.getByText("✕"));
    expect(screen.queryByText("Permission denied")).not.toBeInTheDocument();
  });

  it("calls deleteUser when delete button is clicked and confirmed", async () => {
    const deleteUser = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, deleteUser });
    renderUsers();
    fireEvent.click(screen.getByTitle("Delete user"));
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Alice Smith" } });
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: /delete user/i }));
    await waitFor(() => expect(deleteUser).toHaveBeenCalledWith("user-1"));
  });

  it("does NOT call deleteUser when confirmation is cancelled", async () => {
    const deleteUser = vi.fn().mockResolvedValue(undefined);
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, deleteUser });
    renderUsers();
    fireEvent.click(screen.getByTitle("Delete user"));
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(deleteUser).not.toHaveBeenCalled();
  });

  it("shows actionError when deleteUser throws", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      deleteUser: vi.fn().mockRejectedValue(new Error("Cannot delete")),
    });
    renderUsers();
    fireEvent.click(screen.getByTitle("Delete user"));
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Alice Smith" } });
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: /delete user/i }));
    await waitFor(() => expect(screen.getByText("Cannot delete")).toBeInTheDocument());
  });

  it("shows generic error when deleteUser throws a non-Error value", async () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({
      ...defaultUsersHook,
      deleteUser: vi.fn().mockRejectedValue("plain error"),
    });
    renderUsers();
    fireEvent.click(screen.getByTitle("Delete user"));
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Alice Smith" } });
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: /delete user/i }));
    await waitFor(() => expect(screen.getByText("Failed to delete user.")).toBeInTheDocument());
  });

  it("delete button is disabled for the current user (self)", () => {
    useAuthStore.setState({
      user: { sub: "user-1", email: "alice@example.com", name: "Alice", roles: ["Sovereign"] },
      status: "authenticated",
    });
    renderUsers();
    expect(screen.getByTitle("Delete user")).toBeDisabled();
  });
});

describe("Users refresh", () => {
  it("calls refetch when Refresh button is clicked", () => {
    const refetch = vi.fn();
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, refetch });
    renderUsers();
    fireEvent.click(screen.getByRole("button", { name: /refresh/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it("disables the refresh button when isFetching", () => {
    vi.spyOn(useUsersHook, "default").mockReturnValue({ ...defaultUsersHook, isFetching: true });
    renderUsers();
    expect(screen.getByRole("button", { name: /refresh/i })).toBeDisabled();
  });
});
