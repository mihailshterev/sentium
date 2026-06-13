import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import Skills from "./skills";
import * as useSkillsHook from "../../hooks/useSkills";

const builtIn = { name: "Summarize", description: "Summarizes text", instructions: "Do it well" };
const uploaded = {
  id: "u1",
  name: "Translate",
  description: "Translates text",
  instructions: "Translate carefully",
  skillType: 1,
  fileName: "translate.md",
};

const skillsReturn = {
  skills: [uploaded],
  isLoading: false,
  error: null,
  refetch: vi.fn(),
  builtInSkills: [builtIn],
  isBuiltInLoading: false,
  createSkill: vi.fn(),
  isCreating: false,
  updateSkill: vi.fn(),
  isUpdating: false,
  updatingId: undefined,
  deleteSkill: vi.fn(),
  isDeleting: false,
  deletingId: undefined,
  uploadSkill: vi.fn(),
  isUploading: false,
};

beforeEach(() => {
  vi.spyOn(useSkillsHook, "useSkills").mockReturnValue(
    skillsReturn as unknown as ReturnType<typeof useSkillsHook.useSkills>,
  );
});

describe("Skills page", () => {
  it("renders the title and the built-in tab by default", () => {
    render(<Skills />);
    expect(screen.getByText("Agent Skills")).toBeInTheDocument();
    expect(screen.getByText("Built-in Skills")).toBeInTheDocument();
    expect(screen.getByText("Summarize")).toBeInTheDocument();
  });

  it("switches to the uploaded tab", () => {
    render(<Skills />);
    fireEvent.click(screen.getByTestId("tab-uploaded"));
    expect(screen.getByText("Uploaded Skills")).toBeInTheDocument();
    expect(screen.getByText("Translate")).toBeInTheDocument();
  });

  it("switches to the custom tab", () => {
    render(<Skills />);
    fireEvent.click(screen.getByTestId("tab-custom"));
    expect(screen.queryByText("Built-in Skills")).not.toBeInTheDocument();
  });

  it("expands a built-in skill to show its instructions", () => {
    render(<Skills />);
    fireEvent.click(screen.getByTitle("Toggle instructions"));
    expect(screen.getByText("Do it well")).toBeInTheDocument();
  });
});
