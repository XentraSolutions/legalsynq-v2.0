import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test } from "vitest";
import { FundingNotificationsList } from "./funding-notifications";
import type { OfferedLienRow } from "@/lib/synqlien-funding-portal/types";

const rows: OfferedLienRow[] = [
  {
    id: "offer-1",
    lienNumber: "LN-40218",
    providerName: "Provider",
    sellerName: "John Doe",
    askAmount: 34125,
    offeredAmount: 34125,
    receivedAtUtc: "2026-07-28T07:45:12Z",
    status: "Pending",
    detailHref: "/funding/offered-liens/offer-1",
  },
  {
    id: "offer-2",
    lienNumber: "LN-40220",
    providerName: "Provider",
    sellerName: "David Chen",
    askAmount: 80780,
    offeredAmount: 80780,
    receivedAtUtc: "2026-07-14T02:22:17Z",
    status: "Accepted",
    detailHref: "/funding/offered-liens/offer-2",
  },
];

describe("FundingNotificationsList", () => {
  beforeEach(() => localStorage.clear());

  test("filters real offered-lien notifications by status and search", () => {
    render(<FundingNotificationsList rows={rows} />);

    expect(screen.getByText("LN-40218")).toBeInTheDocument();
    expect(screen.getByText("LN-40220")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Accepted" }));
    expect(screen.queryByText("LN-40218")).not.toBeInTheDocument();
    expect(screen.getByText("LN-40220")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    fireEvent.change(screen.getByPlaceholderText("Search..."), { target: { value: "John" } });
    expect(screen.getByText("LN-40218")).toBeInTheDocument();
    expect(screen.queryByText("LN-40220")).not.toBeInTheDocument();
  });

  test("renders the designed empty state", () => {
    render(<FundingNotificationsList rows={[]} />);
    expect(screen.getByText("No Notifications Yet")).toBeInTheDocument();
  });
});
