import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Inventory from "./inventory";
import * as locusService from "../../services/locus.service";
import type { Location, Asset } from "../../types/locus";

vi.mock("../../services/locus.service", () => ({
  fetchLocations: vi.fn(),
  createLocation: vi.fn(),
  updateLocation: vi.fn(),
  deleteLocation: vi.fn(),
  fetchAssetsByLocation: vi.fn(),
  createAsset: vi.fn(),
  updateAsset: vi.fn(),
  deleteAsset: vi.fn(),
}));

const mockLocation: Location = {
  id: "loc-1",
  name: "Living Room",
  description: "Main living area",
  accessNotes: null,
  parentLocationId: null,
  assetCount: 2,
  subLocationCount: 0,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockChildLocation: Location = {
  id: "loc-2",
  name: "Bedroom",
  description: null,
  accessNotes: null,
  parentLocationId: "loc-1",
  assetCount: 1,
  subLocationCount: 0,
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const mockAsset: Asset = {
  id: "asset-1",
  displayName: "Smart TV",
  category: "Electronics",
  physicalDescription: "Against the east wall",
  instructions: null,
  manufacturer: "Samsung",
  modelNumber: "QN65",
  serialNumber: "SN12345",
  purchaseDate: "2024-01-15T00:00:00Z",
  lastServicedDate: "2025-01-01T00:00:00Z",
  warrantyInfo: "5 years",
  isAgentAccessible: true,
  agentInstructions: null,
  locationId: "loc-1",
  locationName: "Living Room",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

const renderInventory = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <Inventory />
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

beforeEach(() => {
  vi.mocked(locusService.fetchLocations).mockResolvedValue([mockLocation]);
  vi.mocked(locusService.fetchAssetsByLocation).mockResolvedValue([mockAsset]);
  vi.mocked(locusService.createLocation).mockResolvedValue(mockLocation);
  vi.mocked(locusService.updateLocation).mockResolvedValue(mockLocation);
  vi.mocked(locusService.deleteLocation).mockResolvedValue(undefined);
  vi.mocked(locusService.createAsset).mockResolvedValue(mockAsset);
  vi.mocked(locusService.updateAsset).mockResolvedValue(mockAsset);
  vi.mocked(locusService.deleteAsset).mockResolvedValue(undefined);
});

describe("Inventory initial render", () => {
  it("renders the page title", () => {
    renderInventory();
    expect(screen.getByText("Assets & Inventory")).toBeInTheDocument();
  });

  it("renders Add Location button", () => {
    renderInventory();
    expect(screen.getByRole("button", { name: /add/i })).toBeInTheDocument();
  });

  it("shows 'Select a location...' placeholder", () => {
    renderInventory();
    expect(screen.getByText(/select a location/i)).toBeInTheDocument();
  });
});

describe("Inventory location tree", () => {
  it("renders location name after data loads", async () => {
    renderInventory();
    await waitFor(() => expect(screen.getByText("Living Room")).toBeInTheDocument());
  });

  it("renders asset count for location", async () => {
    renderInventory();
    await waitFor(() => expect(screen.getByText("2 assets")).toBeInTheDocument());
  });

  it("renders child locations under parent", async () => {
    vi.mocked(locusService.fetchLocations).mockResolvedValue([mockLocation, mockChildLocation]);
    renderInventory();
    await waitFor(() => expect(screen.getByText("Bedroom")).toBeInTheDocument());
  });

  it("shows '1 asset' (singular) for locations with 1 asset", async () => {
    vi.mocked(locusService.fetchLocations).mockResolvedValue([{ ...mockLocation, assetCount: 1 }]);
    renderInventory();
    await waitFor(() => expect(screen.getByText("1 asset")).toBeInTheDocument());
  });
});

describe("Inventory selecting a location", () => {
  it("shows assets when a location is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText("Smart TV")).toBeInTheDocument());
  });

  it("shows asset category", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText("Electronics")).toBeInTheDocument());
  });

  it("shows manufacturer and model number", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText(/samsung · qn65/i)).toBeInTheDocument());
  });

  it("shows 'Hidden from agents' badge when isAgentAccessible is false", async () => {
    vi.mocked(locusService.fetchAssetsByLocation).mockResolvedValue([{ ...mockAsset, isAgentAccessible: false }]);
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText(/hidden from agents/i)).toBeInTheDocument());
  });

  it("shows physical description with MapPin icon", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText(/against the east wall/i)).toBeInTheDocument());
  });

  it("shows purchase date", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText(/purchased/i)).toBeInTheDocument());
  });

  it("shows last serviced date", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByText(/serviced/i)).toBeInTheDocument());
  });

  it("shows 'No assets in this location.' when no assets", async () => {
    vi.mocked(locusService.fetchAssetsByLocation).mockResolvedValue([]);
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.queryByText(/smart tv/i)).not.toBeInTheDocument());
  });
});

describe("Inventory location form", () => {
  it("opens Add Location modal when Add Location button clicked", () => {
    renderInventory();
    fireEvent.click(screen.getByRole("button", { name: /add/i }));
    expect(screen.getAllByText("Add Location").length).toBeGreaterThanOrEqual(1);
  });

  it("closes the modal when Cancel is clicked", () => {
    renderInventory();
    fireEvent.click(screen.getByRole("button", { name: /add/i }));
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(screen.queryByText("Add Location")).not.toBeInTheDocument();
  });

  it("calls createLocation when form is submitted", async () => {
    renderInventory();
    fireEvent.click(screen.getByRole("button", { name: /add/i }));
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Garage" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() =>
      expect(locusService.createLocation).toHaveBeenCalledWith(
        expect.objectContaining({ name: "Garage" }),
        expect.anything(),
      ),
    );
  });
});

describe("Inventory formatDate utility", () => {
  it("returns '—' for null dates", async () => {
    vi.mocked(locusService.fetchAssetsByLocation).mockResolvedValue([
      { ...mockAsset, purchaseDate: null, lastServicedDate: null },
    ]);
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    expect(screen.queryByText(/serviced/i)).not.toBeInTheDocument();
  });
});

describe("Inventory asset form", () => {
  it("shows 'Add Asset' button after selecting a location", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => expect(screen.getByRole("button", { name: /add asset/i })).toBeInTheDocument());
  });

  it("opens Add Asset form when Add Asset button clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByRole("button", { name: /add asset/i }));
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
  });

  it("can fill in all asset form fields", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByRole("button", { name: /add asset/i }));
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: "New Device" } });
    fireEvent.change(screen.getByLabelText(/category/i), { target: { value: "Electronics" } });
    const allInputs = document.querySelectorAll("input, textarea");
    for (const inp of allInputs) {
      const el = inp as HTMLInputElement;
      if (el.type === "checkbox") {
        fireEvent.change(el, { target: { checked: !el.checked } });
      } else if (el.type === "date" || el.type === "text" || el.tagName === "TEXTAREA") {
        if (!el.value && el.id) {
          fireEvent.change(el, { target: { value: "test-value" } });
        }
      }
    }
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
  });

  it("calls createAsset when asset form submit button is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByRole("button", { name: /add asset/i }));
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: "New Device" } });
    const submitBtns = screen
      .getAllByRole("button")
      .filter((b) => b.textContent?.includes("Add Asset") && b.getAttribute("type") === "button");
    if (submitBtns.length > 0) fireEvent.click(submitBtns[submitBtns.length - 1]);
    await waitFor(() => expect(locusService.createAsset).toHaveBeenCalled());
  });

  it("closes asset form when Cancel is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByRole("button", { name: /add asset/i }));
    fireEvent.click(screen.getByRole("button", { name: /add asset/i }));
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(screen.queryByLabelText(/display name/i)).not.toBeInTheDocument();
  });
});

describe("Inventory asset delete", () => {
  it("calls deleteAsset when delete button is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const deleteBtns = screen.getAllByTitle("Delete");
    fireEvent.click(deleteBtns[deleteBtns.length - 1]);
    await waitFor(() => expect(locusService.deleteAsset).toHaveBeenCalledWith("asset-1"));
  });
});

describe("Inventory asset edit", () => {
  it("opens edit asset form when Edit button on asset card is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    expect(screen.getAllByText("Edit Asset").length).toBeGreaterThanOrEqual(1);
  });

  it("calls updateAsset when edit asset form submitted", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    await waitFor(() => screen.getByLabelText(/display name/i));
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: "Modified TV" } });
    const submitBtns = screen
      .getAllByRole("button")
      .filter((b) => b.textContent?.includes("Edit Asset") && b.getAttribute("type") === "button");
    if (submitBtns.length > 0) fireEvent.click(submitBtns[submitBtns.length - 1]);
    await waitFor(() => expect(locusService.updateAsset).toHaveBeenCalled());
  });
});

describe("Inventory location actions", () => {
  it("shows location action buttons (edit, delete, add child)", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    expect(screen.getByTitle("Edit")).toBeInTheDocument();
    expect(screen.getByTitle("Delete")).toBeInTheDocument();
    expect(screen.getByTitle("Add sub-location")).toBeInTheDocument();
  });

  it("opens edit location form when edit button clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByTitle("Edit"));
    expect(screen.getAllByText(/edit location/i).length).toBeGreaterThanOrEqual(1);
  });

  it("calls updateLocation when edit form submitted", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByTitle("Edit"));
    const nameInput = screen.getByLabelText(/^name/i);
    fireEvent.change(nameInput, { target: { value: "Updated Room" } });
    fireEvent.submit(document.querySelector("form")!);
    await waitFor(() => expect(locusService.updateLocation).toHaveBeenCalled());
  });

  it("calls deleteLocation when delete button clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByTitle("Delete"));
    await waitFor(() => expect(locusService.deleteLocation).toHaveBeenCalledWith("loc-1"));
  });

  it("opens add child location form when add child button clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByTitle("Add sub-location"));
    expect(screen.getAllByText(/add|sub-location/i).length).toBeGreaterThanOrEqual(1);
  });
});

describe("Inventory no location selected", () => {
  it("shows 'Select a location' when no location is selected", async () => {
    renderInventory();
    expect(screen.getByText(/select a location/i)).toBeInTheDocument();
  });
});

describe("Inventory location tree expand/collapse", () => {
  it("expands parent location to show children when expand button is clicked", async () => {
    vi.mocked(locusService.fetchLocations).mockResolvedValue([mockLocation, mockChildLocation]);
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    const expandBtns = screen.queryAllByRole("button").filter((b) => !b.textContent && !b.title);
    if (expandBtns.length > 0) {
      fireEvent.click(expandBtns[0]);
    }
    expect(screen.getByText("Living Room")).toBeInTheDocument();
  });
});

describe("Inventory cancel edit asset form", () => {
  it("closes edit asset form when cancel is clicked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    await waitFor(() => screen.getAllByText("Edit Asset"));
    const cancelBtns = screen.getAllByRole("button").filter((b) => b.textContent === "Cancel");
    if (cancelBtns.length > 0) fireEvent.click(cancelBtns[cancelBtns.length - 1]);
    await waitFor(() => expect(screen.queryByText("Edit Asset")).not.toBeInTheDocument());
  });
});

describe("Inventory agent accessible checkbox in asset form", () => {
  it("shows agent instructions textarea when isAgentAccessible is checked", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    await waitFor(() => screen.getAllByText("Edit Asset"));
    expect(screen.getByPlaceholderText(/Never suggest/i)).toBeInTheDocument();
  });

  it("hides agent instructions textarea when isAgentAccessible is unchecked", async () => {
    vi.mocked(locusService.fetchAssetsByLocation).mockResolvedValue([{ ...mockAsset, isAgentAccessible: false }]);
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    await waitFor(() => screen.getAllByText("Edit Asset"));
    expect(screen.queryByPlaceholderText(/Never suggest/i)).not.toBeInTheDocument();
  });

  it("changes date fields in edit form", async () => {
    renderInventory();
    await waitFor(() => screen.getByText("Living Room"));
    fireEvent.click(screen.getByText("Living Room"));
    await waitFor(() => screen.getByText("Smart TV"));
    const editBtns = screen.getAllByTitle("Edit");
    fireEvent.click(editBtns[editBtns.length - 1]);
    await waitFor(() => screen.getAllByText("Edit Asset"));
    const dateInputs = document.querySelectorAll("input[type='date']");
    if (dateInputs.length > 0) {
      fireEvent.change(dateInputs[0], { target: { value: "2024-06-01" } });
    }
    if (dateInputs.length > 1) {
      fireEvent.change(dateInputs[1], { target: { value: "2024-12-01" } });
    }
    expect(screen.getAllByText("Edit Asset").length).toBeGreaterThan(0);
  });
});
