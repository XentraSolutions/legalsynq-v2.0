import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { OfferedLienResponseDialog } from "./offered-lien-response-dialog";

describe("OfferedLienResponseDialog", () => {
  test("confirms an accepted lien with offer context", () => {
    const onConfirm = vi.fn();

    render(
      <OfferedLienResponseDialog
        action="accept"
        lienNumber="LN-40218"
        sellerName="John Doe"
        sellerCompany="Velantrix"
        askAmount="$34,125.00"
        submitting={false}
        onCancel={vi.fn()}
        onConfirm={onConfirm}
      />,
    );

    expect(screen.getByRole("heading", { name: "Accept This Lien?" })).toBeInTheDocument();
    expect(screen.getByText(/John Doe from Velantrix/)).toBeInTheDocument();
    expect(screen.getByText("$34,125.00")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Yes, Accept" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  test("renders decline treatment and cancels safely", () => {
    const onCancel = vi.fn();

    render(
      <OfferedLienResponseDialog
        action="decline"
        lienNumber="LN-40218"
        sellerName="John Doe"
        submitting={false}
        onCancel={onCancel}
        onConfirm={vi.fn()}
      />,
    );

    expect(screen.getByRole("heading", { name: "Decline This Lien?" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
