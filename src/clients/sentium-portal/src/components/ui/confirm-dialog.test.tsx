import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ConfirmDialog, { type ConfirmDialogProps } from "./confirm-dialog";

const setup = (overrides: Partial<ConfirmDialogProps> = {}) => {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  render(
    <ConfirmDialog
      open
      title="Delete item"
      description="This cannot be undone."
      onConfirm={onConfirm}
      onCancel={onCancel}
      {...overrides}
    />,
  );
  return { onConfirm, onCancel };
};

describe("ConfirmDialog", () => {
  it("renders nothing when closed", () => {
    render(<ConfirmDialog open={false} title="t" description="d" onConfirm={() => {}} onCancel={() => {}} />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders the title and description when open", () => {
    setup();
    expect(screen.getByText("Delete item")).toBeInTheDocument();
    expect(screen.getByText("This cannot be undone.")).toBeInTheDocument();
  });

  it("confirms immediately when no confirm word is required", async () => {
    const { onConfirm } = setup();
    await userEvent.click(screen.getByTestId("confirm-dialog-confirm"));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("cancels when the cancel button is clicked", async () => {
    const { onCancel } = setup();
    await userEvent.click(screen.getByTestId("confirm-dialog-cancel"));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("keeps confirm disabled until the confirm word is typed", async () => {
    const { onConfirm } = setup({ confirmWord: "DELETE" });
    const confirmBtn = screen.getByTestId("confirm-dialog-confirm");
    expect(confirmBtn).toBeDisabled();

    await userEvent.type(screen.getByRole("textbox"), "DELETE");
    expect(confirmBtn).toBeEnabled();

    await userEvent.click(confirmBtn);
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("cancels on the Escape key", async () => {
    const { onCancel } = setup();
    await userEvent.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("confirms on the Enter key when enabled", async () => {
    const { onConfirm } = setup();
    await userEvent.keyboard("{Enter}");
    expect(onConfirm).toHaveBeenCalledOnce();
  });
});
