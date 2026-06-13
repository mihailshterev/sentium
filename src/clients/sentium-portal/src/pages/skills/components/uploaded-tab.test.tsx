import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import UploadedTab from "./uploaded-tab";
import * as useSkillsHook from "../../../hooks/useSkills";

const uploaded = {
  id: "u1",
  name: "translate",
  description: "translates",
  instructions: "do it",
  skillType: 1,
  fileName: "translate.md",
};

const uploadSkill = vi.fn().mockResolvedValue(undefined);
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
    createSkill: vi.fn(),
    isCreating: false,
    updateSkill,
    isUpdating: false,
    updatingId: undefined,
    deleteSkill,
    isDeleting: false,
    deletingId: undefined,
    uploadSkill,
    isUploading: false,
  } as unknown as ReturnType<typeof useSkillsHook.useSkills>);

beforeEach(() => {
  uploadSkill.mockClear();
  updateSkill.mockClear();
  deleteSkill.mockClear();
  setSkills([uploaded]);
});

describe("UploadedTab", () => {
  it("renders the header and an uploaded skill", () => {
    render(<UploadedTab />);
    expect(screen.getByText("Uploaded Skills")).toBeInTheDocument();
    expect(screen.getByText("translate")).toBeInTheDocument();
  });

  it("shows an empty state when there are no uploaded skills", () => {
    setSkills([]);
    render(<UploadedTab />);
    expect(screen.getByText(/no uploaded skills yet/i)).toBeInTheDocument();
  });

  it("uploads a markdown file", async () => {
    const { container } = render(<UploadedTab />);
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(["# skill"], "new.md", { type: "text/markdown" });
    fireEvent.change(input, { target: { files: [file] } });
    await waitFor(() => expect(uploadSkill).toHaveBeenCalledWith(file));
  });

  it("edits and saves a skill", async () => {
    render(<UploadedTab />);
    fireEvent.click(screen.getByTitle("Edit"));

    const nameInput = screen.getByDisplayValue("translate");
    fireEvent.change(nameInput, { target: { value: "translate-v2" } });
    fireEvent.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() =>
      expect(updateSkill).toHaveBeenCalledWith({
        id: "u1",
        payload: { name: "translate-v2", description: "translates", instructions: "do it" },
      }),
    );
  });

  it("deletes a skill", () => {
    render(<UploadedTab />);
    fireEvent.click(screen.getByTitle("Delete"));
    expect(deleteSkill).toHaveBeenCalledWith("u1");
  });
});
