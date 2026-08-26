import { render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { CaseInformationFields } from "./case-information-fields";

vi.mock("@/components/selling/selling-entity-select", () => ({
  SellingEntitySelect: ({ placeholder }: { placeholder?: string }) => (
    <div>{placeholder}</div>
  ),
}));

describe("CaseInformationFields", () => {
  test("only captures lien-owned associations", () => {
    render(
      <CaseInformationFields
        value={{
          medicalProviderId: "",
          fundingCompanyId: "",
          fundingCompanyContactId: "",
        }}
        onChange={() => undefined}
      />,
    );

    expect(screen.getByText("Medical Provider")).toBeInTheDocument();
    expect(screen.getByText("Funding Company")).toBeInTheDocument();
    expect(screen.getByText("Contact Person")).toBeInTheDocument();
    expect(screen.queryByText("Handling Law Firm")).not.toBeInTheDocument();
    expect(screen.queryByText("Case Manager")).not.toBeInTheDocument();
    expect(screen.queryByText("Medical Facility")).not.toBeInTheDocument();
  });
});
