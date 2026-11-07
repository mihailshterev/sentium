import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { assetSummary, loc } from "../test-fixtures";
import type { AssetFilters } from "../types/inventory";
import { AssetFilterBar } from "./asset-filter-bar";
import { AssetFormDialog } from "./asset-form-dialog";
import { AssetList } from "./asset-list";
import { AttachmentsPanel } from "./attachments-panel";
import { DeleteLocationDialog } from "./delete-location-dialog";
import { KnowledgeStatusBadge } from "./knowledge-status-badge";
import { LocationFormDialog } from "./location-form-dialog";
import { LocationTree } from "./location-tree";
import { TagInput } from "./tag-input";

const locations = [
  loc("home", "Home", null, "Home", { childCount: 2, subtreeAssetCount: 3 }),
  loc("garage", "Garage", "home", "Home › Garage", { childCount: 1, assetCount: 2, subtreeAssetCount: 3 }),
  loc("shelf", "Shelf B", "garage", "Home › Garage › Shelf B", { assetCount: 1, subtreeAssetCount: 1 }),
  loc("kitchen", "Kitchen", "home", "Home › Kitchen"),
];

describe("LocationTree", () => {
  const renderTree = (selectedId: string | null = null) => {
    const handlers = { onSelect: vi.fn(), onAddChild: vi.fn(), onEdit: vi.fn(), onDelete: vi.fn() };
    render(<LocationTree locations={locations} selectedId={selectedId} {...handlers} />);
    return handlers;
  };

  it("renders an accessible tree with levels, counts and the all-locations row", () => {
    renderTree();

    expect(screen.getByRole("tree", { name: "Locations" })).toBeInTheDocument();
    expect(screen.getByRole("treeitem", { name: /all locations/i })).toHaveAttribute("aria-selected", "true");
    const garage = screen.getByRole("treeitem", { name: "Garage" });
    expect(garage).toHaveAttribute("aria-level", "3");
    expect(garage).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("treeitem", { name: "Shelf B" })).toBeInTheDocument();
  });

  it("selects on click and exposes row actions", () => {
    const handlers = renderTree();

    fireEvent.click(screen.getByRole("treeitem", { name: "Kitchen" }));
    expect(handlers.onSelect).toHaveBeenCalledWith("kitchen");

    fireEvent.click(screen.getByRole("button", { name: "Add a location inside Garage" }));
    expect(handlers.onAddChild).toHaveBeenCalledWith(expect.objectContaining({ id: "garage" }));
    fireEvent.click(screen.getByRole("button", { name: "Edit Garage" }));
    expect(handlers.onEdit).toHaveBeenCalledWith(expect.objectContaining({ id: "garage" }));
    fireEvent.click(screen.getByTestId("location-delete-Garage"));
    expect(handlers.onDelete).toHaveBeenCalledWith(expect.objectContaining({ id: "garage" }));
    expect(handlers.onSelect).toHaveBeenCalledTimes(1);
  });

  it("collapses and expands with the toggle", () => {
    renderTree();

    fireEvent.click(screen.getByRole("button", { name: "Collapse Garage" }));
    expect(screen.queryByRole("treeitem", { name: "Shelf B" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Expand Garage" }));
    expect(screen.getByRole("treeitem", { name: "Shelf B" })).toBeInTheDocument();
  });

  it("supports keyboard navigation", () => {
    const handlers = renderTree();
    const tree = screen.getByRole("tree");

    screen.getByRole("treeitem", { name: /all locations/i }).focus();
    fireEvent.keyDown(tree, { key: "ArrowDown" });
    expect(screen.getByRole("treeitem", { name: "Home" })).toHaveFocus();
    fireEvent.keyDown(tree, { key: "ArrowRight" });
    expect(screen.getByRole("treeitem", { name: "Garage" })).toHaveFocus();
    fireEvent.keyDown(tree, { key: "ArrowLeft" });
    expect(screen.queryByRole("treeitem", { name: "Shelf B" })).not.toBeInTheDocument();
    fireEvent.keyDown(tree, { key: "ArrowLeft" });
    expect(screen.getByRole("treeitem", { name: "Home" })).toHaveFocus();
    fireEvent.keyDown(tree, { key: "End" });
    expect(screen.getByRole("treeitem", { name: "Kitchen" })).toHaveFocus();
    fireEvent.keyDown(tree, { key: "Enter" });
    expect(handlers.onSelect).toHaveBeenCalledWith("kitchen");
    fireEvent.keyDown(tree, { key: "Home" });
    fireEvent.keyDown(tree, { key: " " });
    expect(handlers.onSelect).toHaveBeenLastCalledWith(null);
  });

  it("keeps the path to the selection expanded", () => {
    renderTree("shelf");

    fireEvent.click(screen.getByRole("button", { name: "Collapse Garage" }));
    expect(screen.getByRole("treeitem", { name: "Shelf B" })).toHaveAttribute("aria-selected", "true");
  });
});

describe("TagInput", () => {
  function Harness({ initial = [] as string[] }) {
    const [tags, setTags] = useState(initial);
    return <TagInput aria-label="Tags" value={tags} onChange={setTags} suggestions={["tools", "garden"]} maxTags={3} />;
  }

  it("adds normalized tags with Enter and commas, ignoring duplicates", () => {
    render(<Harness />);
    const input = screen.getByRole("combobox", { name: "Tags" });

    fireEvent.change(input, { target: { value: "  Power  Tools" } });
    fireEvent.keyDown(input, { key: "Enter" });
    fireEvent.change(input, { target: { value: "garden," } });
    fireEvent.change(input, { target: { value: "POWER TOOLS" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(screen.getByText("power tools")).toBeInTheDocument();
    expect(screen.getByText("garden")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /remove tag/i })).toHaveLength(2);
  });

  it("removes tags with the chip button or Backspace and respects the limit", () => {
    render(<Harness initial={["a", "b", "c"]} />);

    expect(screen.getByRole("combobox", { name: "Tags" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Remove tag b" }));
    const input = screen.getByRole("combobox", { name: "Tags" });
    fireEvent.keyDown(input, { key: "Backspace" });

    expect(screen.queryByText("b")).not.toBeInTheDocument();
    expect(screen.queryByText("c")).not.toBeInTheDocument();
    expect(screen.getByText("a")).toBeInTheDocument();
  });
});

describe("KnowledgeStatusBadge", () => {
  it("labels each status and offers a retry after a failure", () => {
    const onRetry = vi.fn();
    const { rerender } = render(<KnowledgeStatusBadge status="Synced" />);
    expect(screen.getByTestId("knowledge-status")).toHaveTextContent("Shared");

    rerender(<KnowledgeStatusBadge status="NotShared" />);
    expect(screen.getByTestId("knowledge-status")).toHaveTextContent("Private");

    rerender(<KnowledgeStatusBadge status="Failed" error="embedding server down" onRetry={onRetry} />);
    expect(screen.getByTestId("knowledge-status")).toHaveAttribute("title", "embedding server down");
    fireEvent.click(screen.getByRole("button", { name: /retry/i }));
    expect(onRetry).toHaveBeenCalled();
  });
});

describe("DeleteLocationDialog", () => {
  it("deletes an empty location directly", () => {
    const onConfirm = vi.fn();
    render(
      <DeleteLocationDialog
        location={locations[3]}
        locations={locations}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    expect(screen.getByText(/this location is empty/i)).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));
    expect(onConfirm).toHaveBeenCalledWith(null);
  });

  it("asks where the contents of a non-empty location should go, defaulting to its parent", () => {
    const onConfirm = vi.fn();
    render(
      <DeleteLocationDialog
        location={locations[1]}
        locations={locations}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    expect(screen.getByText(/1 sub-location and 2 items/i)).toBeInTheDocument();
    const select = screen.getByLabelText("Move its contents to");
    const options = within(select)
      .getAllByRole("option")
      .map((o) => o.textContent);
    expect(options).not.toContain("Home › Garage › Shelf B");
    expect(select).toHaveValue("home");

    fireEvent.change(select, { target: { value: "kitchen" } });
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));
    expect(onConfirm).toHaveBeenCalledWith("kitchen");
  });

  it("requires a target for a non-empty root location", () => {
    render(
      <DeleteLocationDialog
        location={locations[0]}
        locations={locations}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    expect(screen.getByTestId("confirm-dialog-confirm")).toBeDisabled();
  });
});

describe("LocationFormDialog", () => {
  it("submits a new location under the preselected parent", async () => {
    const onSubmit = vi.fn();
    render(
      <LocationFormDialog
        locations={locations}
        parentId="garage"
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    fireEvent.change(screen.getByLabelText("Name"), { target: { value: " Drawer " } });
    fireEvent.click(screen.getByRole("button", { name: "Create location" }));

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        name: "Drawer",
        parentLocationId: "garage",
        description: null,
        accessNotes: null,
      }),
    );
  });

  it("excludes the edited location's own subtree from the parent choices", () => {
    render(
      <LocationFormDialog
        locations={locations}
        location={locations[1]}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    const options = within(screen.getByLabelText("Inside"))
      .getAllByRole("option")
      .map((o) => o.getAttribute("value"));
    expect(options).toEqual(["", "home", "kitchen"]);
  });

  it("shows validation errors", async () => {
    const onSubmit = vi.fn();
    render(<LocationFormDialog locations={locations} onSubmit={onSubmit} onCancel={vi.fn()} isPending={false} />);

    fireEvent.click(screen.getByRole("button", { name: "Create location" }));

    expect(await screen.findByText("Name is required")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});

describe("AssetFormDialog", () => {
  it("submits the asset with tags and privacy settings", async () => {
    const onSubmit = vi.fn();
    render(
      <AssetFormDialog
        locations={locations}
        defaultLocationId="garage"
        categories={["Tools"]}
        tagSuggestions={["power"]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        isPending={false}
      />,
    );

    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Drill" } });
    const tags = screen.getByLabelText("Tags");
    fireEvent.change(tags, { target: { value: "power" } });
    fireEvent.keyDown(tags, { key: "Enter" });
    expect(screen.getByLabelText("Notes for assistants")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("switch"));
    expect(screen.queryByLabelText("Notes for assistants")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Create asset" }));

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          displayName: "Drill",
          locationId: "garage",
          tags: ["power"],
          isAgentAccessible: false,
        }),
      ),
    );
  });
});

describe("AssetFilterBar", () => {
  it("debounces the search and reports filter changes", async () => {
    const onChange = vi.fn();
    const filters: AssetFilters = { q: "", locationId: "garage", tags: [] };
    render(
      <AssetFilterBar
        filters={filters}
        onChange={onChange}
        categories={["Tools"]}
        tags={[{ name: "power", count: 2 }]}
      />,
    );

    fireEvent.change(screen.getByLabelText("Search assets"), { target: { value: "drill" } });
    await waitFor(() => expect(onChange).toHaveBeenCalledWith({ q: "drill" }));

    fireEvent.change(screen.getByLabelText("Category"), { target: { value: "Tools" } });
    expect(onChange).toHaveBeenCalledWith({ category: "Tools" });
    fireEvent.change(screen.getByLabelText("Shared with assistants"), { target: { value: "private" } });
    expect(onChange).toHaveBeenCalledWith({ agentAccessible: false });
    fireEvent.change(screen.getByLabelText("Sort"), { target: { value: "Updated" } });
    expect(onChange).toHaveBeenCalledWith({ sort: "Updated" });
    fireEvent.click(screen.getByRole("switch"));
    expect(onChange).toHaveBeenCalledWith({ includeSubLocations: false });
  });
});

describe("AssetList", () => {
  const renderList = (props: Partial<React.ComponentProps<typeof AssetList>> = {}) =>
    render(
      <MemoryRouter>
        <AssetList
          assets={[]}
          isLoading={false}
          isError={false}
          hasMore={false}
          isLoadingMore={false}
          onLoadMore={vi.fn()}
          hasFilters={false}
          {...props}
        />
      </MemoryRouter>,
    );

  it("links each asset to its detail page with path, tags and status", () => {
    renderList({ assets: [assetSummary({ attachmentCount: 2, tags: ["a", "b", "c", "d"] })] });

    const row = screen.getByTestId("asset-row-Cordless drill");
    expect(row).toHaveAttribute("href", "/inventory/assets/asset-1");
    expect(within(row).getByText("Home › Garage")).toBeInTheDocument();
    expect(within(row).getByText("+1")).toBeInTheDocument();
    expect(within(row).getByText("Shared")).toBeInTheDocument();
  });

  it("distinguishes empty results from an empty location", () => {
    const { unmount } = renderList({ hasFilters: true });
    expect(screen.getByText("No matching assets")).toBeInTheDocument();
    unmount();

    renderList();
    expect(screen.getByText("Nothing catalogued here yet")).toBeInTheDocument();
  });

  it("shows loading and error states", () => {
    const { unmount } = renderList({ isError: true });
    expect(screen.getByText("Failed to load assets.")).toBeInTheDocument();
    unmount();
    renderList({ isLoading: true });
    expect(document.querySelector("[aria-busy='true']")).toBeInTheDocument();
  });
});

describe("AttachmentsPanel", () => {
  const image = {
    id: "att-1",
    fileName: "front.png",
    contentType: "image/png",
    sizeBytes: 2048,
    kind: "Photo" as const,
    isImage: true,
    contentUrl: "/api/locus/assets/asset-1/attachments/att-1/content",
    createdAt: "2026-01-01T00:00:00Z",
    createdBy: null,
  };
  const manual = {
    ...image,
    id: "att-2",
    fileName: "manual.pdf",
    contentType: "application/pdf",
    kind: "Manual" as const,
    isImage: false,
  };

  it("uploads a picked file with a detected kind", () => {
    const onUpload = vi.fn();
    render(<AttachmentsPanel attachments={[]} onUpload={onUpload} onDelete={vi.fn()} isUploading={false} />);

    const file = new File(["%PDF"], "manual.pdf", { type: "application/pdf" });
    fireEvent.change(screen.getByTestId("attachment-input"), { target: { files: [file] } });

    expect(onUpload).toHaveBeenCalledWith(file, "Manual");
    expect(screen.getByText("No attachments yet.")).toBeInTheDocument();
  });

  it("rejects files over the size limit before uploading", () => {
    const onUpload = vi.fn();
    render(<AttachmentsPanel attachments={[]} onUpload={onUpload} onDelete={vi.fn()} isUploading={false} />);

    const big = new File(["x"], "huge.jpg", { type: "image/jpeg" });
    Object.defineProperty(big, "size", { value: 26 * 1024 * 1024 });
    fireEvent.change(screen.getByTestId("attachment-input"), { target: { files: [big] } });

    expect(onUpload).not.toHaveBeenCalled();
    expect(screen.getByText(/larger than 25 MB/)).toBeInTheDocument();
  });

  it("shows photos and files, previews images and confirms deletes", () => {
    const onDelete = vi.fn();
    render(
      <AttachmentsPanel attachments={[image, manual]} onUpload={vi.fn()} onDelete={onDelete} isUploading={false} />,
    );

    expect(screen.getByRole("link", { name: "Download manual.pdf" })).toHaveAttribute(
      "href",
      `${manual.contentUrl}?download=true`,
    );
    fireEvent.click(screen.getByRole("button", { name: "View front.png" }));
    expect(screen.getByText("Download original")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("attachment-delete-manual.pdf"));
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));
    expect(onDelete).toHaveBeenCalledWith(manual);
  });
});
