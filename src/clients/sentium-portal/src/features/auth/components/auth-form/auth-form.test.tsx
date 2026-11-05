import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { AuthForm } from "./auth-form";

afterEach(() => vi.unstubAllGlobals());

it("submits typed values after switching to register mode", async () => {
  const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ({ redirectUrl: "/" }) });
  vi.stubGlobal("fetch", fetchMock);
  Object.defineProperty(window, "location", {
    value: { ...window.location, search: "", assign: vi.fn() },
    writable: true,
  });

  render(<AuthForm />);
  fireEvent.click(screen.getByRole("button", { name: "Register" }));
  fireEvent.change(document.getElementById("userName")!, { target: { value: "alice" } });
  fireEvent.change(document.getElementById("password")!, { target: { value: "abcdefghijklmn" } });
  fireEvent.click(screen.getByRole("button", { name: /Create account/ }));

  await waitFor(() => expect(fetchMock).toHaveBeenCalled());
  expect(screen.queryByText(/Username must be/)).toBeNull();
  expect(screen.queryByText(/at least 8/)).toBeNull();
  expect(fetchMock.mock.calls[0][0]).toContain("/identity/account/register");
});
