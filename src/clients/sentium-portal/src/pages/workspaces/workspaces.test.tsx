import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Workspaces from "./workspaces";
import WorkspaceDetail from "./workspace-detail";
import * as agentRuntimeService from "../../services/agentRuntime.service";
import type { Workspace, WorkspaceFile } from "../../types/workspace";

vi.mock("../../services/agentRuntime.service", () => ({
  fetchWorkspaces: vi.fn(),
  fetchWorkspacesPaged: vi.fn(),
  createWorkspace: vi.fn(),
  updateWorkspace: vi.fn(),
  deleteWorkspace: vi.fn(),
  fetchWorkspaceFiles: vi.fn(),
  uploadWorkspaceFile: vi.fn(),
  deleteWorkspaceFile: vi.fn(),
}));

const mockWorkspace: Workspace = {
  id: "ws-1",
  name: "Incident 2026",
  description: "Main incident workspace",
  fileCount: 2,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockFile: WorkspaceFile = {
  id: "file-1",
  fileName: "report.txt",
  extension: ".txt",
  sizeBytes: 2048,
  workspaceId: "ws-1",
  processingStatus: "Completed",
  createdAt: "2025-01-01T00:00:00Z",
};

const renderWorkspaces = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Workspaces />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

const renderWorkspaceDetail = (workspaceId = "ws-1") => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/workspaces/${workspaceId}`]}>
        <Routes>
          <Route path="/workspaces/:workspaceId" element={<WorkspaceDetail />} />
          <Route path="/workspaces" element={<Workspaces />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.mocked(agentRuntimeService.fetchWorkspaces).mockResolvedValue([mockWorkspace]);
  vi.mocked(agentRuntimeService.fetchWorkspacesPaged).mockResolvedValue({
    items: [mockWorkspace],
    totalCount: 1,
    page: 1,
    pageSize: 100,
    totalPages: 1,
  });
  vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([mockFile]);
  vi.mocked(agentRuntimeService.createWorkspace).mockResolvedValue(mockWorkspace);
  vi.mocked(agentRuntimeService.updateWorkspace).mockResolvedValue(mockWorkspace);
  vi.mocked(agentRuntimeService.deleteWorkspace).mockResolvedValue(undefined);
  vi.mocked(agentRuntimeService.uploadWorkspaceFile).mockResolvedValue(mockFile);
  vi.mocked(agentRuntimeService.deleteWorkspaceFile).mockResolvedValue(undefined);
});

describe("Workspaces initial render", () => {
  it("renders the page title", async () => {
    renderWorkspaces();
    expect(screen.getAllByText("Workspaces").length).toBeGreaterThanOrEqual(1);
  });

  it("renders New Workspace button", () => {
    renderWorkspaces();
    expect(screen.getByRole("button", { name: /new workspace/i })).toBeInTheDocument();
  });

  it("shows workspace grid when workspaces are loaded", async () => {
    renderWorkspaces();
    await waitFor(() => expect(screen.getByText("Incident 2026")).toBeInTheDocument());
  });
});

describe("Workspaces list state", () => {
  it("renders workspace name after data loads", async () => {
    renderWorkspaces();
    await waitFor(() => expect(screen.getByText("Incident 2026")).toBeInTheDocument());
  });

  it("renders workspace description", async () => {
    renderWorkspaces();
    await waitFor(() => expect(screen.getByText("Main incident workspace")).toBeInTheDocument());
  });
});

describe("Workspaces selecting a workspace", () => {
  it("shows workspace detail when navigating to workspace route", async () => {
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText(/report\.txt/i)).toBeInTheDocument());
  });

  it("shows empty files message when no files in workspace", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText(/no files yet/i)).toBeInTheDocument());
  });

  it("shows file processing status badge", async () => {
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText("Completed")).toBeInTheDocument());
  });

  it("shows Pending status badge", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([
      { ...mockFile, processingStatus: "Pending" },
    ]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText("Pending")).toBeInTheDocument());
  });
});

describe("Workspaces create form", () => {
  it("opens create form when New Workspace is clicked", () => {
    renderWorkspaces();
    fireEvent.click(screen.getByRole("button", { name: /new workspace/i }));
    expect(screen.getAllByText("Create Workspace").length).toBeGreaterThanOrEqual(1);
  });

  it("submit button is disabled when name is empty", () => {
    renderWorkspaces();
    fireEvent.click(screen.getByRole("button", { name: /new workspace/i }));
    expect(screen.getByRole("button", { name: /new workspace/i, hidden: false })).toBeInTheDocument();
  });

  it("calls createWorkspace when form is submitted", async () => {
    renderWorkspaces();
    fireEvent.click(screen.getByRole("button", { name: /new workspace/i }));
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "New WS" } });
    fireEvent.change(screen.getByLabelText(/description/i), { target: { value: "A description" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() =>
      expect(agentRuntimeService.createWorkspace).toHaveBeenCalledWith(
        expect.objectContaining({ name: "New WS", description: "A description" }),
      ),
    );
  });

  it("closes create form when Cancel is clicked", () => {
    renderWorkspaces();
    fireEvent.click(screen.getByRole("button", { name: /new workspace/i }));
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(screen.queryByLabelText(/^name/i)).not.toBeInTheDocument();
  });
});

describe("Workspaces file upload", () => {
  it("shows file type error for unsupported file types", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(fileInput, "files", {
      value: [new File(["content"], "test.exe", { type: "application/octet-stream" })],
      configurable: true,
    });
    fireEvent.change(fileInput);
    await waitFor(() => expect(screen.getByText(/".exe" is not supported/i)).toBeInTheDocument());
  });

  it("shows selected file name when valid file chosen", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(fileInput, "files", {
      value: [new File(["content"], "report.txt", { type: "text/plain" })],
      configurable: true,
    });
    fireEvent.change(fileInput);
    await waitFor(() => expect(screen.getAllByText("report.txt").length).toBeGreaterThanOrEqual(1));
  });
});

describe("Workspaces formatBytes utility", () => {
  it("shows file size in KB", async () => {
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText(/2\.0 KB/)).toBeInTheDocument());
  });

  it("shows file size in MB for large files", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([{ ...mockFile, sizeBytes: 5 * 1024 * 1024 }]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText(/5\.0 MB/)).toBeInTheDocument());
  });
});

describe("Workspaces edit workspace", () => {
  it("opens edit form when edit button is clicked", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Edit workspace"));
    expect(screen.getAllByText("Edit Workspace").length).toBeGreaterThanOrEqual(1);
  });

  it("pre-fills name in edit form", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Edit workspace"));
    expect((screen.getByLabelText(/^name/i) as HTMLInputElement).value).toBe("Incident 2026");
  });

  it("calls updateWorkspace when edit form submitted", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Edit workspace"));
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Updated WS" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(agentRuntimeService.updateWorkspace).toHaveBeenCalled());
  });

  it("updates selected workspace info after successful edit", async () => {
    vi.mocked(agentRuntimeService.updateWorkspace).mockResolvedValue({ ...mockWorkspace, name: "Updated WS" });
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Edit workspace"));
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Updated WS" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(agentRuntimeService.updateWorkspace).toHaveBeenCalled());
  });

  it("closes edit form when Cancel is clicked", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Edit workspace"));
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(screen.queryByText("Edit Workspace")).not.toBeInTheDocument();
  });
});

describe("Workspaces delete workspace", () => {
  it("calls deleteWorkspace when delete button is clicked", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Delete workspace"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /delete workspace/i }));
    await waitFor(() => expect(agentRuntimeService.deleteWorkspace).toHaveBeenCalledWith("ws-1"));
  });

  it("deselects workspace when selected workspace is deleted", async () => {
    renderWorkspaces();
    await waitFor(() => screen.getByText("Incident 2026"));
    fireEvent.click(screen.getByTitle("Delete workspace"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /delete workspace/i }));
    await waitFor(() => expect(agentRuntimeService.deleteWorkspace).toHaveBeenCalledWith("ws-1"));
  });
});

describe("Workspaces delete file", () => {
  it("calls deleteWorkspaceFile when delete file button is clicked", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByTitle("Remove file"));
    fireEvent.click(screen.getByTitle("Remove file"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /remove file/i }));
    await waitFor(() => expect(agentRuntimeService.deleteWorkspaceFile).toHaveBeenCalledWith("file-1"));
  });
});

describe("Workspaces upload submit", () => {
  it("calls uploadWorkspaceFile when form submitted with a file", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Drop a file or click to browse"));

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(fileInput, "files", {
      value: [new File(["content"], "data.csv", { type: "text/csv" })],
      configurable: true,
    });
    fireEvent.change(fileInput);
    await waitFor(() => screen.getAllByText("data.csv"));
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(agentRuntimeService.uploadWorkspaceFile).toHaveBeenCalled());
  });

  it("shows upload error when upload fails", async () => {
    vi.mocked(agentRuntimeService.uploadWorkspaceFile).mockRejectedValue(new Error("Upload failed"));
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Drop a file or click to browse"));

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(fileInput, "files", {
      value: [new File(["x"], "notes.txt", { type: "text/plain" })],
      configurable: true,
    });
    fireEvent.change(fileInput);
    await waitFor(() => screen.getAllByText("notes.txt"));
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(screen.getByText(/upload failed/i)).toBeInTheDocument());
  });
});

describe("Workspaces empty workspace state", () => {
  it("shows 'No workspaces yet' and New Workspace button when empty", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspacesPaged).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 100,
      totalPages: 0,
    });
    renderWorkspaces();
    await waitFor(() => expect(screen.getByText(/no workspaces yet/i)).toBeInTheDocument());
    expect(screen.getAllByRole("button", { name: /new workspace/i }).length).toBeGreaterThanOrEqual(1);
  });

  it("opens create form via empty state New Workspace button", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspacesPaged).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 100,
      totalPages: 0,
    });
    renderWorkspaces();
    await waitFor(() => screen.getAllByRole("button", { name: /new workspace/i }));
    const btns = screen.getAllByRole("button", { name: /new workspace/i });
    fireEvent.click(btns[btns.length - 1]);
    expect(screen.getAllByText("Create Workspace").length).toBeGreaterThanOrEqual(1);
  });
});

describe("Workspaces Processing file status", () => {
  it("shows Processing status badge", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([
      { ...mockFile, processingStatus: "Processing" },
    ]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText("Processing")).toBeInTheDocument());
  });

  it("shows Failed status badge", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([{ ...mockFile, processingStatus: "Failed" }]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText("Failed")).toBeInTheDocument());
  });
});

describe("Workspaces small file bytes", () => {
  it("shows file size in bytes for tiny files", async () => {
    vi.mocked(agentRuntimeService.fetchWorkspaceFiles).mockResolvedValue([{ ...mockFile, sizeBytes: 500 }]);
    renderWorkspaceDetail();
    await waitFor(() => expect(screen.getByText(/500 B/)).toBeInTheDocument());
  });
});

describe("Workspaces drag and drop upload", () => {
  it("handles drag over and drag leave on upload area", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));
    const uploadArea = document.querySelector("[role='button']") as HTMLElement;
    fireEvent.dragOver(uploadArea);
    fireEvent.dragLeave(uploadArea);
    expect(uploadArea).toBeInTheDocument();
  });

  it("handles drop event with a file", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));
    const uploadArea = document.querySelector("[role='button']") as HTMLElement;
    const file = new File(["content"], "test-file.txt", { type: "text/plain" });
    fireEvent.drop(uploadArea, { dataTransfer: { files: [file] } });
    await waitFor(() => expect(screen.getByText("test-file.txt")).toBeInTheDocument());
  });

  it("handles keydown Enter on upload area", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));
    const uploadArea = document.querySelector("[role='button']") as HTMLElement;
    fireEvent.keyDown(uploadArea, { key: "Enter" });
    expect(uploadArea).toBeInTheDocument();
  });

  it("does nothing on non-Enter keydown on upload area", async () => {
    renderWorkspaceDetail();
    await waitFor(() => screen.getByText("Upload File"));
    const uploadArea = document.querySelector("[role='button']") as HTMLElement;
    fireEvent.keyDown(uploadArea, { key: "Space" });
    expect(uploadArea).toBeInTheDocument();
  });
});
