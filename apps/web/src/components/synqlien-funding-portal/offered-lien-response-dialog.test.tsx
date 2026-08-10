import { act, fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { OfferedLienResponseAlert, OfferedLienResponseDialog } from "./offered-lien-response-dialog";

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

describe("OfferedLienResponseAlert", () => {
  test.each([
    ["accept", "Lien Accepted!", "Offered lien has been successfully purchased and accepted."],
    ["decline", "Lien Declined!", "Offered lien has been successfully declined."],
  ] as const)("renders the %s confirmation copy", (action, title, description) => {
    render(<OfferedLienResponseAlert action={action} onDismiss={vi.fn()} />);

    expect(screen.getByText(title)).toBeInTheDocument();
    expect(screen.getByText(description)).toBeInTheDocument();
  });

  test("fades and dismisses automatically", () => {
    vi.useFakeTimers();
    const onDismiss = vi.fn();

    render(<OfferedLienResponseAlert action="accept" onDismiss={onDismiss} />);

    act(() => vi.advanceTimersByTime(4500));
    expect(screen.getByRole("status")).toHaveClass("opacity-0");
    expect(onDismiss).not.toHaveBeenCalled();

    act(() => vi.advanceTimersByTime(300));
    expect(onDismiss).toHaveBeenCalledTimes(1);
    vi.useRealTimers();
  });
});
