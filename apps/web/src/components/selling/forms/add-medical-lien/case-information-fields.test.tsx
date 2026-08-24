import { render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { CaseInformationFields } from "./case-information-fields";

vi.mock("@/components/selling/selling-entity-select", () => ({
  SellingEntitySelect: ({ placeholder }: { placeholder?: string }) => (
    <div>{placeholder}</div>
  ),
}));

describe("CaseInformationFields", () => {
  test("matches the create-lien design without a medical facility field", () => {
    render(
      <CaseInformationFields
        value={{
          medicalProviderId: "",
          fundingCompanyId: "",
          fundingCompanyContactId: "",
          lawfirmId: "",
          caseManagerId: "",
        }}
        onChange={() => undefined}
      />,
    );

    expect(screen.getByText("Medical Provider")).toBeInTheDocument();
    expect(screen.getByText("Funding Company")).toBeInTheDocument();
    expect(screen.getByText("Contact Person")).toBeInTheDocument();
    expect(screen.getByText("Handling Law Firm")).toBeInTheDocument();
    expect(screen.getByText("Case Manager")).toBeInTheDocument();
    expect(screen.queryByText("Medical Facility")).not.toBeInTheDocument();
  });
});
