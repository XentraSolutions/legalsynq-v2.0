import { render, screen } from "@testing-library/react";
import type React from "react";
import { describe, expect, test, vi } from "vitest";
import { ProviderFundingDetailsPanel } from "./provider-funding-details-panel";
import { LienInformationPanel } from "./lien-information-panel";
import { MedicalCodesInformationPanel } from "./medical-codes-information-panel";

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    ...props
  }: React.AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe("bulk-imported lien details", () => {
  test("renders the canonical CSV values under their matching UI labels", () => {
    render(
      <>
        <LienInformationPanel
          lien={{
            lienNumber: "SL-10001",
            sellerStatus: "Internal",
            status: "Draft",
            purchaseDate: "2026-07-15",
            initialServiceDate: "2026-07-19",
            endServiceDate: "2026-07-22",
            listingVisibility: "Public",
            notes: "Imported detail notes",
            buyerMessage: null,
          }}
          caseInformation={{
            id: "case-id",
            caseNumber: "CASE-MAPPED-001",
            title: null,
            caseManagerId: null,
            caseManagerName: null,
            lawFirmId: null,
            lawFirm: null,
          }}
        />
        <ProviderFundingDetailsPanel
          fundingCompany={null}
          facility={{ id: null, name: "Sunrise Clinic", emailAddress: null }}
          medicalProvider={{ id: null, name: "City Medical Center" }}
        />
        <MedicalCodesInformationPanel
          lien={[
            {
              id: "pricing-id",
              description: "45385",
              notes: JSON.stringify({
                medicalCode: "45385",
                description: "Colonoscopy",
                medicareCost: 879,
                billingAmount: 250,
                targetSaleAmount: 175,
              }),
              createdAtUtc: "2026-07-19T00:00:00Z",
            },
          ]}
        />
      </>,
    );

    expect(screen.getByText("Case ID")).toBeInTheDocument();
    expect(screen.getByText("CASE-MAPPED-001")).toBeInTheDocument();
    expect(screen.getByText("Sunrise Clinic")).toBeInTheDocument();
    expect(screen.getByText("City Medical Center")).toBeInTheDocument();
    expect(screen.getByText("Medicare Cost")).toBeInTheDocument();
    expect(screen.getByText("$879.00")).toBeInTheDocument();
    expect(screen.getByText("$175.00")).toBeInTheDocument();
  });

  test("shows a fallback for the medical facility field when no facility is assigned", () => {
    render(
      <ProviderFundingDetailsPanel
        fundingCompany={null}
        facility={null}
        medicalProvider={{ id: "provider-id", name: "City Medical Center" }}
      />,
    );

    expect(screen.getByText("Medical Facility")).toBeInTheDocument();
  });
});
