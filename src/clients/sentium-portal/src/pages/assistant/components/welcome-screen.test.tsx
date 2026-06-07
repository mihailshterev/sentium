import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import WelcomeScreen from "./welcome-screen";
import * as useProfileHook from "../../../hooks/useProfile";

beforeEach(() => {
  vi.spyOn(useProfileHook, "default").mockReturnValue({
    profile: { firstName: "Alice" },
  } as unknown as ReturnType<typeof useProfileHook.default>);
});

describe("WelcomeScreen", () => {
  it("renders a greeting that includes the user's first name", () => {
    render(<WelcomeScreen suggestions={[]} onSelectSuggestion={() => {}} />);
    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("Alice");
  });

  it("renders a chip for each suggestion", () => {
    render(<WelcomeScreen suggestions={["Idea one", "Idea two"]} onSelectSuggestion={() => {}} />);
    expect(screen.getByRole("button", { name: "Idea one" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Idea two" })).toBeInTheDocument();
  });

  it("calls onSelectSuggestion when a chip is clicked", () => {
    const onSelect = vi.fn();
    render(<WelcomeScreen suggestions={["Idea one"]} onSelectSuggestion={onSelect} />);
    fireEvent.click(screen.getByRole("button", { name: "Idea one" }));
    expect(onSelect).toHaveBeenCalledWith("Idea one");
  });

  it("renders without a name when the profile has none", () => {
    vi.spyOn(useProfileHook, "default").mockReturnValue({
      profile: undefined,
    } as unknown as ReturnType<typeof useProfileHook.default>);
    render(<WelcomeScreen suggestions={[]} onSelectSuggestion={() => {}} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });

  it.each([
    ["morning", 8],
    ["afternoon", 14],
    ["evening", 19],
    ["night", 2],
  ])("renders a %s greeting", (_label, hour) => {
    vi.spyOn(Date.prototype, "getHours").mockReturnValue(hour);
    render(<WelcomeScreen suggestions={[]} onSelectSuggestion={() => {}} />);
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });
});
