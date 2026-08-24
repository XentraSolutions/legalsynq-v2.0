import { render, screen } from "@testing-library/react";
import { describe, expect, test } from "vitest";
import { FundingCompanyAndCaseInformationPanel } from "./funding-company-information-panel";
import { LienInformationPanel } from "./lien-information-panel";
import { MedicalCodesInformationPanel } from "./medical-codes-information-panel";

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
        />
        <FundingCompanyAndCaseInformationPanel
          fundingCompany={null}
          facility={{ id: null, name: "Sunrise Clinic", emailAddress: null }}
          medicalProvider={{ id: null, name: "City Medical Center" }}
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

    expect(screen.getByText("Purchase Date")).toBeInTheDocument();
    expect(screen.getByText("2026-07-15")).toBeInTheDocument();
    expect(screen.getByText("Sunrise Clinic")).toBeInTheDocument();
    expect(screen.getByText("City Medical Center")).toBeInTheDocument();
    expect(screen.getByText("Medicare Cost")).toBeInTheDocument();
    expect(screen.getByText("$879.00")).toBeInTheDocument();
    expect(screen.getByText("$175.00")).toBeInTheDocument();
  });

  test("omits the medical facility field when no facility is assigned", () => {
    render(
      <FundingCompanyAndCaseInformationPanel
        fundingCompany={null}
        facility={null}
        medicalProvider={{ id: "provider-id", name: "City Medical Center" }}
        caseInformation={null}
      />,
    );

    expect(screen.queryByText("Medical Facility")).not.toBeInTheDocument();
  });
});
