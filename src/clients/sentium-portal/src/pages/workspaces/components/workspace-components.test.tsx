import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { render, screen, fireEvent, within } from "@testing-library/react";
import UploadPanel from "./upload-panel";
import WorkspaceFileCard from "./workspace-file-card";
import WorkspaceSidebar from "./workspace-sidebar";
import type { Workspace, WorkspaceFile } from "../../../types/workspace";

const uploadDefaults = {
  workspaceName: "Docs",
  selectedFile: null as File | null,
  dragActive: false,
  fileTypeError: null as string | null,
  isUploading: false,
  isUploadSuccess: false,
  isUploadError: false,
  uploadError: null as Error | null,
  acceptedExtensions: ".txt,.md",
  onDrop: vi.fn(),
  onDragOver: vi.fn(),
  onDragLeave: vi.fn(),
  onInputChange: vi.fn(),
  onSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
  fileInputRef: createRef<HTMLInputElement>(),
};

describe("UploadPanel", () => {
  it("prompts to drop a file when none is selected and disables submit", () => {
    render(<UploadPanel {...uploadDefaults} />);
    expect(screen.getByText(/drop a file or click to browse/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /upload to docs/i })).toBeDisabled();
  });

  it("shows the selected file name and enables submit", () => {
    const file = new File(["x"], "notes.md");
    render(<UploadPanel {...uploadDefaults} selectedFile={file} />);
    expect(screen.getByText("notes.md")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /upload to docs/i })).toBeEnabled();
  });

  it("surfaces a file-type error", () => {
    render(<UploadPanel {...uploadDefaults} fileTypeError="Unsupported type" />);
    expect(screen.getByText("Unsupported type")).toBeInTheDocument();
  });

  it("shows uploading, success and error states", () => {
    const { rerender } = render(<UploadPanel {...uploadDefaults} isUploading />);
    expect(screen.getByText(/uploading/i)).toBeInTheDocument();
    rerender(<UploadPanel {...uploadDefaults} isUploadSuccess />);
    expect(screen.getByText(/indexing in background/i)).toBeInTheDocument();
    rerender(<UploadPanel {...uploadDefaults} isUploadError uploadError={new Error("boom")} />);
    expect(screen.getByText("boom")).toBeInTheDocument();
  });

  it("submits the form", () => {
    const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
    const file = new File(["x"], "a.md");
    const { container } = render(<UploadPanel {...uploadDefaults} selectedFile={file} onSubmit={onSubmit} />);
    fireEvent.submit(container.querySelector("form")!);
    expect(onSubmit).toHaveBeenCalled();
  });
});

describe("WorkspaceFileCard", () => {
  const file = {
    id: "f1",
    fileName: "report.md",
    sizeBytes: 2048,
    createdAt: "2025-01-01T00:00:00Z",
    processingStatus: "Completed",
  } as WorkspaceFile;

  it("renders the file name and status", () => {
    render(<WorkspaceFileCard file={file} onDelete={vi.fn()} />);
    expect(screen.getByText("report.md")).toBeInTheDocument();
    expect(screen.getByText("Completed")).toBeInTheDocument();
  });

  it("confirms before deleting", async () => {
    const onDelete = vi.fn();
    render(<WorkspaceFileCard file={file} onDelete={onDelete} />);
    fireEvent.click(screen.getByTitle("Delete file"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByTestId("confirm-dialog-confirm"));
    expect(onDelete).toHaveBeenCalledWith("f1");
  });
});

describe("WorkspaceSidebar", () => {
  const ws = (overrides: Partial<Workspace> = {}): Workspace =>
    ({ id: "w1", name: "Alpha", description: "first", fileCount: 2, ...overrides }) as Workspace;

  it("shows an empty state and a create action", () => {
    const onCreateNew = vi.fn();
    render(
      <WorkspaceSidebar
        workspaces={[]}
        selectedWorkspace={null}
        isWorkspacesError={false}
        onSelect={vi.fn()}
        onEdit={vi.fn()}
        onDelete={vi.fn()}
        onCreateNew={onCreateNew}
      />,
    );
    expect(screen.getByText(/no workspaces yet/i)).toBeInTheDocument();
    fireEvent.click(screen.getByText("Create one"));
    expect(onCreateNew).toHaveBeenCalled();
  });

  it("shows an error message when loading failed", () => {
    render(
      <WorkspaceSidebar
        workspaces={[]}
        selectedWorkspace={null}
        isWorkspacesError
        onSelect={vi.fn()}
        onEdit={vi.fn()}
        onDelete={vi.fn()}
        onCreateNew={vi.fn()}
      />,
    );
    expect(screen.getByText(/failed to load workspaces/i)).toBeInTheDocument();
  });

  it("selects and edits a workspace", () => {
    const onSelect = vi.fn();
    const onEdit = vi.fn();
    render(
      <WorkspaceSidebar
        workspaces={[ws()]}
        selectedWorkspace={null}
        isWorkspacesError={false}
        onSelect={onSelect}
        onEdit={onEdit}
        onDelete={vi.fn()}
        onCreateNew={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByText("Alpha"));
    expect(onSelect).toHaveBeenCalled();
    fireEvent.click(screen.getByTitle("Edit workspace"));
    expect(onEdit).toHaveBeenCalled();
  });

  it("requires typing the name to confirm deletion", async () => {
    const onDelete = vi.fn();
    render(
      <WorkspaceSidebar
        workspaces={[ws()]}
        selectedWorkspace={null}
        isWorkspacesError={false}
        onSelect={vi.fn()}
        onEdit={vi.fn()}
        onDelete={onDelete}
        onCreateNew={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByTitle("Delete workspace"));
    const dialog = await screen.findByRole("dialog");
    const confirm = within(dialog).getByTestId("confirm-dialog-confirm");
    expect(confirm).toBeDisabled();
    fireEvent.change(within(dialog).getByRole("textbox"), { target: { value: "Alpha" } });
    fireEvent.click(confirm);
    expect(onDelete).toHaveBeenCalledWith("w1");
  });
});
