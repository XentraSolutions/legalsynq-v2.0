import { render, screen } from "@testing-library/react";
import { describe, expect, test } from "vitest";

import type { FundingApplicationDetail } from "@/types/fund";
import { FundingApplicationDetailPanel } from "./funding-application-detail-panel";

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

describe("FundingApplicationDetailPanel", () => {
  test("renders the Application identity and status in the restored header", () => {
    render(<FundingApplicationDetailPanel application={application} />);

    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(screen.getByText("APP-0001")).toBeInTheDocument();
    expect(screen.getByText("APP-0001").parentElement?.parentElement).toHaveClass(
      "items-start",
      "justify-between",
    );
    expect(screen.queryByRole("link", { name: "Open in App" })).not.toBeInTheDocument();
  });

  test("preserves the Application details and applicant summary", () => {
    render(<FundingApplicationDetailPanel application={application} />);

    expect(screen.getAllByText("Ada Lovelace")).toHaveLength(2);
    expect(screen.getByText("$10,000")).toBeInTheDocument();
    expect(screen.getAllByText("Personal Injury")).toHaveLength(2);
  });
});
