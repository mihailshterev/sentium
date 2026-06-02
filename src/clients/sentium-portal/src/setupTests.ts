/// <reference types="vitest/globals" />
import "@testing-library/jest-dom";

if (typeof window.matchMedia !== "function") {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    configurable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }),
  });
}

const locationStub = {
  href: "",
  pathname: "/",
  origin: "http://localhost",
  assign: vi.fn(),
  replace: vi.fn(),
  reload: vi.fn(),
};

Object.defineProperty(window, "location", {
  value: locationStub,
  writable: true,
  configurable: true,
});

const originalConsoleError = console.error.bind(console);

beforeAll(() => {
  console.error = (...args: unknown[]) => {
    const msg = typeof args[0] === "string" ? args[0] : "";
    if (msg.includes("act(") || msg.includes("ReactDOMTestUtils")) return;
    originalConsoleError(...args);
  };
});

afterAll(() => {
  console.error = originalConsoleError;
});

afterEach(() => {
  vi.restoreAllMocks();
  locationStub.href = "";
  locationStub.pathname = "/";
  locationStub.assign.mockReset();
  locationStub.replace.mockReset();
  locationStub.reload.mockReset();
});
