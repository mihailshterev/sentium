import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import * as identityService from "@/features/auth/api/identity.service";
import { ApiError } from "@/shared/api/client";
import { SecurityCard } from "./security-card";

const renderCard = () => {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <SecurityCard />
    </QueryClientProvider>,
  );
};

const fill = (current: string, next: string, repeat: string) => {
  fireEvent.change(screen.getByLabelText("Current password"), { target: { value: current } });
  fireEvent.change(screen.getByLabelText("New password"), { target: { value: next } });
  fireEvent.change(screen.getByLabelText("Repeat new password"), { target: { value: repeat } });
  fireEvent.click(screen.getByRole("button", { name: /change password/i }));
};

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("SecurityCard", () => {
  it("changes the password", async () => {
    const change = vi.spyOn(identityService, "changePassword").mockResolvedValue(undefined);
    renderCard();
    fill("old-pass-1", "new-pass-2", "new-pass-2");
    await waitFor(() => expect(change).toHaveBeenCalledWith({ currentPassword: "old-pass-1", newPassword: "new-pass-2" }));
    expect(await screen.findByText("Password changed.")).toBeInTheDocument();
  });

  it("rejects a repeat that does not match", async () => {
    const change = vi.spyOn(identityService, "changePassword").mockResolvedValue(undefined);
    renderCard();
    fill("old-pass-1", "new-pass-2", "new-pass-3");
    expect(await screen.findByText("The passwords do not match")).toBeInTheDocument();
    expect(change).not.toHaveBeenCalled();
  });

  it("shows why the server refused the change", async () => {
    vi.spyOn(identityService, "changePassword").mockRejectedValue(new ApiError(400, "Incorrect password."));
    renderCard();
    fill("wrong-pass-1", "new-pass-2", "new-pass-2");
    expect(await screen.findByText("Incorrect password.")).toBeInTheDocument();
  });

  it("signs out the other browsers", async () => {
    const signOut = vi.spyOn(identityService, "signOutOtherSessions").mockResolvedValue(undefined);
    renderCard();
    fireEvent.click(screen.getByRole("button", { name: /sign out other browsers/i }));
    await waitFor(() => expect(signOut).toHaveBeenCalled());
    expect(await screen.findByText(/signed out within a minute/)).toBeInTheDocument();
  });
});
