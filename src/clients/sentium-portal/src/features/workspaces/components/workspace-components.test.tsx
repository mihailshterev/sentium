import { describe, it, expect, vi } from "vitest";
import { createRef } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { UploadPanel } from "./upload-panel";

const uploadDefaults = {
  workspaceName: "Docs",
  selectedFiles: [] as File[],
  dragActive: false,
  problems: [] as string[],
  isUploading: false,
  uploadProgress: null as number | null,
  isUploadSuccess: false,
  isUploadError: false,
  uploadError: null as Error | null,
  rules: { extensions: [".md", ".pdf"], maxFileSizeBytes: 100 * 1024 * 1024 },
  onDrop: vi.fn(),
  onDragOver: vi.fn(),
  onDragLeave: vi.fn(),
  onInputChange: vi.fn(),
  onSubmit: vi.fn((e: React.FormEvent) => e.preventDefault()),
  fileInputRef: createRef<HTMLInputElement>(),
};

describe("UploadPanel", () => {
  it("prompts to drop files when none are selected and disables submit", () => {
    render(<UploadPanel {...uploadDefaults} />);
    expect(screen.getByText(/drop files or click to browse/i)).toBeInTheDocument();
    expect(screen.getByText(/pdf and word files up to 100\.0 MB each/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /upload to docs/i })).toBeDisabled();
  });

  it("shows the selected file name and enables submit", () => {
    const file = new File(["x"], "notes.md");
    render(<UploadPanel {...uploadDefaults} selectedFiles={[file]} />);
    expect(screen.getByText("notes.md")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /upload to docs/i })).toBeEnabled();
  });

  it("summarises several selected files", () => {
    render(<UploadPanel {...uploadDefaults} selectedFiles={[new File(["x"], "a.md"), new File(["y"], "b.md")]} />);
    expect(screen.getByText("2 files selected")).toBeInTheDocument();
  });

  it("lists why files were not selected", () => {
    render(<UploadPanel {...uploadDefaults} problems={['"a.exe": .exe files are not supported.']} />);
    expect(screen.getByText('"a.exe": .exe files are not supported.')).toBeInTheDocument();
  });

  it("restricts the file chooser to the accepted types", () => {
    const { container } = render(<UploadPanel {...uploadDefaults} />);
    const input = container.querySelector('input[type="file"]')!;
    expect(input).toHaveAttribute("accept", ".md,.pdf");
    expect(input).toHaveAttribute("multiple");
  });

  it("shows upload progress, success and error states", () => {
    const { rerender } = render(<UploadPanel {...uploadDefaults} isUploading uploadProgress={0.5} />);
    expect(screen.getByText("Uploading… 50%")).toBeInTheDocument();
    rerender(<UploadPanel {...uploadDefaults} isUploadSuccess />);
    expect(screen.getByText(/indexing in background/i)).toBeInTheDocument();
    rerender(<UploadPanel {...uploadDefaults} isUploadError uploadError={new Error("boom")} />);
    expect(screen.getByText("boom")).toBeInTheDocument();
  });

  it("submits the form", () => {
    const onSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
    const file = new File(["x"], "a.md");
    const { container } = render(<UploadPanel {...uploadDefaults} selectedFiles={[file]} onSubmit={onSubmit} />);
    fireEvent.submit(container.querySelector("form")!);
    expect(onSubmit).toHaveBeenCalled();
  });
});
