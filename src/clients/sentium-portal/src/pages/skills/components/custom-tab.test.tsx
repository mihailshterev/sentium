import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import CustomTab from "./custom-tab";
import * as useSkillsHook from "../../../hooks/useSkills";

const customSkill = {
  id: "c1",
  name: "my-skill",
  description: "does things",
  instructions: "step 1",
  skillType: 0,
};

const createSkill = vi.fn().mockResolvedValue(undefined);
const updateSkill = vi.fn().mockResolvedValue(undefined);
const deleteSkill = vi.fn().mockResolvedValue(undefined);

const setSkills = (skills: unknown[]) =>
  vi.spyOn(useSkillsHook, "useSkills").mockReturnValue({
    skills,
    isLoading: false,
    error: null,
    refetch: vi.fn(),
    builtInSkills: [],
    isBuiltInLoading: false,
    createSkill,
    isCreating: false,
    updateSkill,
    isUpdating: false,
    updatingId: undefined,
    deleteSkill,
    isDeleting: false,
    deletingId: undefined,
    uploadSkill: vi.fn(),
    isUploading: false,
  } as unknown as ReturnType<typeof useSkillsHook.useSkills>);

beforeEach(() => {
  createSkill.mockClear();
  updateSkill.mockClear();
  deleteSkill.mockClear();
  setSkills([customSkill]);
});

describe("CustomTab", () => {
  it("renders the header and existing custom skills", () => {
    render(<CustomTab />);
    expect(screen.getByText("Custom Skills")).toBeInTheDocument();
    expect(screen.getByText("my-skill")).toBeInTheDocument();
  });

  it("shows an empty state when there are no custom skills", () => {
    setSkills([]);
    render(<CustomTab />);
    expect(screen.getByText(/no custom skills yet/i)).toBeInTheDocument();
  });

  it("opens the new-skill form and creates a skill", async () => {
    setSkills([]);
    render(<CustomTab />);
    fireEvent.click(screen.getByRole("button", { name: /new skill/i }));

    fireEvent.change(screen.getByPlaceholderText("my-custom-skill"), { target: { value: "name" } });
    fireEvent.change(screen.getByPlaceholderText(/when to use this skill/i), { target: { value: "desc" } });
    fireEvent.change(screen.getByPlaceholderText(/step-by-step guidance/i), { target: { value: "do it" } });

    fireEvent.click(screen.getByRole("button", { name: /create/i }));
    await waitFor(() =>
      expect(createSkill).toHaveBeenCalledWith({
        name: "name",
        description: "desc",
        instructions: "do it",
        skillType: 0,
      }),
    );
  });

  it("does not submit when required fields are blank", () => {
    setSkills([]);
    render(<CustomTab />);
    fireEvent.click(screen.getByRole("button", { name: /new skill/i }));
    fireEvent.click(screen.getByRole("button", { name: /create/i }));
    expect(createSkill).not.toHaveBeenCalled();
  });

  it("opens a confirm dialog and deletes a skill", async () => {
    render(<CustomTab />);
    fireEvent.click(screen.getByTitle(/delete/i));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/delete custom skill/i)).toBeInTheDocument();
    fireEvent.click(within(dialog).getByTestId("confirm-dialog-confirm"));

    await waitFor(() => expect(deleteSkill).toHaveBeenCalledWith("c1"));
  });
});
