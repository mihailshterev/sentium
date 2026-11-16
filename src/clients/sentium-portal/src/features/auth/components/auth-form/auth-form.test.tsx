import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { AuthForm } from "./auth-form";

afterEach(() => vi.unstubAllGlobals());

const respond = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body });

const serverWith = (registration: { open: boolean; bootstrap: boolean }) =>
  vi.fn((url: string) =>
    Promise.resolve(url.endsWith("/identity/account/registration") ? respond(registration) : respond({ redirectUrl: "/" })),
  );

it("offers only sign-in while registration is closed", async () => {
  const fetchMock = serverWith({ open: false, bootstrap: false });
  vi.stubGlobal("fetch", fetchMock);

  render(<AuthForm />);

  await waitFor(() => expect(fetchMock).toHaveBeenCalled());
  expect(screen.queryByRole("button", { name: "Register" })).toBeNull();
  expect(screen.getByRole("button", { name: "Sign in" })).toBeTruthy();
});

it("explains that the first account becomes the Sovereign", async () => {
  vi.stubGlobal("fetch", serverWith({ open: true, bootstrap: true }));

  render(<AuthForm />);

  expect(await screen.findByText(/first account you register becomes the Sovereign/)).toBeTruthy();
});

it("submits typed values after switching to register mode", async () => {
  const fetchMock = serverWith({ open: true, bootstrap: false });
  vi.stubGlobal("fetch", fetchMock);
  Object.defineProperty(window, "location", {
    value: { ...window.location, search: "", assign: vi.fn() },
    writable: true,
  });

  render(<AuthForm />);
  fireEvent.click(await screen.findByRole("button", { name: "Register" }));
  fireEvent.change(document.getElementById("userName")!, { target: { value: "alice" } });
  fireEvent.change(document.getElementById("password")!, { target: { value: "abcdefghijklmn" } });
  fireEvent.click(screen.getByRole("button", { name: /Create account/ }));

  await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
  expect(screen.queryByText(/Username must be/)).toBeNull();
  expect(screen.queryByText(/at least 8/)).toBeNull();
  expect(fetchMock.mock.calls[1][0]).toContain("/identity/account/register");
});
