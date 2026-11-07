import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import * as hooks from "../hooks/useInventory";
import { asset, assetSummary, loc } from "../test-fixtures";
import { AssetDetailView } from "./asset-detail-view";
import { InventoryView } from "./inventory-view";

vi.mock("../hooks/useInventory", () => ({
  useLocations: vi.fn(),
  useLocationMutations: vi.fn(),
  useAssetSearch: vi.fn(),
  useAsset: vi.fn(),
  useAssetMutations: vi.fn(),
  useAttachmentMutations: vi.fn(),
  useTaxonomy: vi.fn(),
}));

const mutation = () => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, error: null });

const locations = [
  loc("home", "Home", null, "Home", { childCount: 1, subtreeAssetCount: 1 }),
  loc("garage", "Garage", "home", "Home › Garage", { assetCount: 1, subtreeAssetCount: 1 }),
];

function CurrentUrl() {
  const location = useLocation();
  return <output data-testid="url">{location.pathname + location.search}</output>;
}

const renderAt = (url: string) =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/inventory" element={<InventoryView />} />
        <Route path="/inventory/assets/:assetId" element={<AssetDetailView />} />
      </Routes>
      <CurrentUrl />
    </MemoryRouter>,
  );

describe("inventory views", () => {
  let locationMutations: ReturnType<typeof hooks.useLocationMutations>;
  let assetMutations: ReturnType<typeof hooks.useAssetMutations>;
  let attachmentMutations: ReturnType<typeof hooks.useAttachmentMutations>;

  beforeEach(() => {
    locationMutations = { create: mutation(), update: mutation(), remove: mutation() } as never;
    assetMutations = { create: mutation(), update: mutation(), remove: mutation(), retrySync: mutation() } as never;
    attachmentMutations = { upload: mutation(), remove: mutation() } as never;

    vi.mocked(hooks.useLocations).mockReturnValue({ locations, isLoading: false, isError: false });
    vi.mocked(hooks.useLocationMutations).mockReturnValue(locationMutations);
    vi.mocked(hooks.useAssetMutations).mockReturnValue(assetMutations);
    vi.mocked(hooks.useAttachmentMutations).mockReturnValue(attachmentMutations);
    vi.mocked(hooks.useTaxonomy).mockReturnValue({ tags: [{ name: "power", count: 1 }], categories: ["Tools"] });
    vi.mocked(hooks.useAssetSearch).mockReturnValue({
      assets: [assetSummary()],
      totalCount: 1,
      isLoading: false,
      isError: false,
      hasMore: false,
      loadMore: vi.fn(),
      isLoadingMore: false,
    } as never);
    vi.mocked(hooks.useAsset).mockReturnValue({ asset: asset(), isLoading: false, error: null });
  });

  it("shows the tree and the assets, and filters by the selected location through the URL", () => {
    renderAt("/inventory");

    expect(screen.getByRole("heading", { name: "Inventory" })).toBeInTheDocument();
    expect(screen.getByRole("treeitem", { name: "Garage" })).toBeInTheDocument();
    expect(screen.getByTestId("asset-row-Cordless drill")).toBeInTheDocument();
    expect(screen.getByText("1 item")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("treeitem", { name: "Garage" }));

    expect(screen.getByTestId("url")).toHaveTextContent("/inventory?location=garage");
    expect(hooks.useAssetSearch).toHaveBeenLastCalledWith(expect.objectContaining({ locationId: "garage" }));
    // The list header now shows the selected path (the asset row shows it too).
    expect(screen.getAllByText("Home › Garage").length).toBeGreaterThan(1);
  });

  it("reads tag and sharing filters from the URL", () => {
    renderAt("/inventory?tag=power&tag=diy&shared=false&sort=Updated");

    expect(hooks.useAssetSearch).toHaveBeenLastCalledWith(
      expect.objectContaining({ tags: ["power", "diy"], agentAccessible: false, sort: "Updated" }),
    );
  });

  it("creates a location under the selected one", async () => {
    renderAt("/inventory?location=garage");

    fireEvent.click(screen.getByRole("button", { name: "New location" }));
    fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Shelf" } });
    fireEvent.click(screen.getByRole("button", { name: "Create location" }));

    await waitFor(() =>
      expect(locationMutations.create.mutate).toHaveBeenCalledWith(
        expect.objectContaining({ name: "Shelf", parentLocationId: "garage" }),
        expect.anything(),
      ),
    );
  });

  it("deletes a location with its contents moved to the parent", () => {
    renderAt("/inventory");

    fireEvent.click(screen.getByTestId("location-delete-Garage"));
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));

    expect(locationMutations.remove.mutate).toHaveBeenCalledWith(
      { id: "garage", moveContentsTo: "home" },
      expect.anything(),
    );
  });

  it("opens the asset form", () => {
    renderAt("/inventory?location=garage");

    fireEvent.click(screen.getByRole("button", { name: "New asset" }));

    expect(screen.getByRole("dialog", { name: "New asset" })).toBeInTheDocument();
    expect(screen.getByLabelText("Kept at")).toHaveValue("garage");
  });

  it("shows an empty state when there are no locations", () => {
    vi.mocked(hooks.useLocations).mockReturnValue({ locations: [], isLoading: false, isError: false });
    renderAt("/inventory");

    expect(screen.getByText("No locations yet")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New asset" })).toBeDisabled();
  });

  it("renders an asset's details, breadcrumb and assistant notes", () => {
    renderAt("/inventory/assets/asset-1");

    expect(screen.getByRole("heading", { name: "Cordless drill" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Garage" })).toHaveAttribute("href", "/inventory?location=garage");
    expect(screen.getByText("GSR 12V")).toBeInTheDocument();
    expect(screen.getByText("Wear goggles")).toBeInTheDocument();
    expect(screen.getByTestId("knowledge-status")).toHaveTextContent("Shared");
  });

  it("deletes an asset after confirmation and returns to its location", () => {
    renderAt("/inventory/assets/asset-1");

    fireEvent.click(screen.getByTestId("asset-delete-Cordless drill"));
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));

    expect(assetMutations.remove.mutate).toHaveBeenCalledWith("asset-1", expect.anything());
  });

  it("retries a failed knowledge sync", () => {
    vi.mocked(hooks.useAsset).mockReturnValue({
      asset: asset({ knowledgeStatus: "Failed", knowledgeError: "embedding server down" }),
      isLoading: false,
      error: null,
    });
    renderAt("/inventory/assets/asset-1");

    expect(screen.getByText("Last error: embedding server down")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /retry/i }));
    expect(assetMutations.retrySync.mutate).toHaveBeenCalledWith("asset-1");
  });

  it("explains a private asset and a missing asset", () => {
    vi.mocked(hooks.useAsset).mockReturnValue({
      asset: asset({ isAgentAccessible: false, knowledgeStatus: "NotShared" }),
      isLoading: false,
      error: null,
    });
    const { unmount } = renderAt("/inventory/assets/asset-1");
    expect(screen.getByText(/this item is private/i)).toBeInTheDocument();
    unmount();

    vi.mocked(hooks.useAsset).mockReturnValue({ asset: null, isLoading: false, error: new Error("boom") });
    renderAt("/inventory/assets/asset-1");
    expect(screen.getByText("Could not load the asset")).toBeInTheDocument();
  });
});
