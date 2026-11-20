import { describe, it, expect } from "vitest";
import { fireEvent, render } from "@testing-library/react";
import { Avatar } from "./avatar";
import { initialsOf } from "./initials";

describe("initialsOf", () => {
  it("takes the first letters of the first and last word", () => {
    expect(initialsOf("Ada Byron Lovelace")).toBe("AL");
  });

  it("takes two letters of a single word", () => {
    expect(initialsOf("mihail")).toBe("MI");
  });

  it("falls back to a question mark for an empty name", () => {
    expect(initialsOf("  ")).toBe("?");
  });
});

describe("Avatar", () => {
  it("shows initials when there is no picture", () => {
    const { container } = render(<Avatar name="Alice Smith" />);
    expect(container.textContent).toBe("AS");
    expect(container.querySelector("img")).toBeNull();
  });

  it("shows the picture when there is one", () => {
    const { container } = render(<Avatar name="Alice Smith" src="/api/identity/users/u1/avatar?v=1" />);
    expect(container.querySelector("img")?.getAttribute("src")).toBe("/api/identity/users/u1/avatar?v=1");
  });

  it("falls back to initials when the picture fails to load", () => {
    const { container } = render(<Avatar name="Alice Smith" src="/broken" />);
    fireEvent.error(container.querySelector("img")!);
    expect(container.querySelector("img")).toBeNull();
    expect(container.textContent).toBe("AS");
  });

  it("tries again when the picture changes", () => {
    const { container, rerender } = render(<Avatar name="Alice Smith" src="/old" />);
    fireEvent.error(container.querySelector("img")!);
    rerender(<Avatar name="Alice Smith" src="/new" />);
    expect(container.querySelector("img")?.getAttribute("src")).toBe("/new");
  });
});
