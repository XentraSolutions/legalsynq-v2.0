import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test } from "vitest";

import type { FundingApplicationDetail } from "@/types/fund";
import { FundingApplicationDetailPanel } from "./funding-application-detail-panel";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

afterEach(() => {
  if (originalBaseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = originalBaseUrl;
  }
});

const application: FundingApplicationDetail = {
  id: "11111111-1111-1111-1111-111111111111",
  tenantId: "tenant-1",
  applicationNumber: "APP-0001",
  applicantFirstName: "Ada",
  applicantLastName: "Lovelace",
  email: "ada@example.test",
  phone: "555-0100",
  requestedAmount: 10000,
  caseType: "Personal Injury",
  status: "Draft",
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-02T00:00:00Z",
};

describe("FundingApplicationDetailPanel Open in App integration", () => {
  test("places Open in App beside the existing status header", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";

    render(<FundingApplicationDetailPanel application={application} />);

    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(screen.getByText("APP-0001")).toBeInTheDocument();
    expect(screen.getByText("APP-0001").parentElement?.parentElement).toHaveClass(
      "flex-col",
      "sm:flex-row",
    );
    expect(screen.getByRole("link", { name: "Open in App" })).toHaveAttribute(
      "href",
      "https://links.example.test/applications/11111111-1111-1111-1111-111111111111",
    );
  });

  test("preserves Application details when configuration is unavailable", () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(<FundingApplicationDetailPanel application={application} />);

    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(screen.getByText("APP-0001")).toBeInTheDocument();
    expect(screen.getAllByText("Ada Lovelace")).toHaveLength(2);
    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });
});
