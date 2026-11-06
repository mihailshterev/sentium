import { describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Dialog } from "./dialog";

const renderDialog = (props: Partial<React.ComponentProps<typeof Dialog>> = {}) => {
  const onClose = vi.fn();
  render(
    <Dialog open title="Edit agent" description="Change the agent" onClose={onClose} {...props}>
      <input aria-label="Name" />
      <button type="button">Inner</button>
    </Dialog>,
  );
  return { onClose };
};

describe("Dialog", () => {
  it("renders nothing when closed", () => {
    render(
      <Dialog open={false} title="Hidden" onClose={() => {}}>
        x
      </Dialog>,
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders a labelled, described modal dialog", () => {
    renderDialog();
    const dialog = screen.getByRole("dialog", { name: "Edit agent" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAccessibleDescription("Change the agent");
  });

  it("focuses the first focusable element on open", () => {
    renderDialog();
    expect(screen.getByRole("button", { name: "Close" })).toHaveFocus();
  });

  it("closes on Escape, the close button and a backdrop click", async () => {
    const { onClose } = renderDialog();
    await userEvent.keyboard("{Escape}");
    await userEvent.click(screen.getByRole("button", { name: "Close" }));
    fireEvent.click(document.querySelector("[class*='modalOverlay']")!);
    expect(onClose).toHaveBeenCalledTimes(3);
  });

  it("ignores clicks inside the panel", () => {
    const { onClose } = renderDialog();
    fireEvent.click(screen.getByRole("dialog"));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("does not dismiss when dismissible is false", async () => {
    const { onClose } = renderDialog({ dismissible: false });
    await userEvent.keyboard("{Escape}");
    fireEvent.click(document.querySelector("[class*='modalOverlay']")!);
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Close" })).not.toBeInTheDocument();
  });

  it("keeps Tab focus inside the dialog", async () => {
    renderDialog({ hideCloseButton: true });
    const input = screen.getByRole("textbox", { name: "Name" });
    const inner = screen.getByRole("button", { name: "Inner" });
    expect(input).toHaveFocus();
    await userEvent.tab();
    expect(inner).toHaveFocus();
    await userEvent.tab();
    expect(input).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(inner).toHaveFocus();
  });

  it("renders the footer", () => {
    renderDialog({ footer: <button type="button">Save</button> });
    expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
  });
});
