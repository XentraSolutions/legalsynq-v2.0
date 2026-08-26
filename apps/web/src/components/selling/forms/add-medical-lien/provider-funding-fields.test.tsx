import { render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { ProviderFundingFields } from "./provider-funding-fields";

vi.mock("@/components/selling/selling-entity-select", () => ({
  SellingEntitySelect: ({ placeholder }: { placeholder?: string }) => (
    <div>{placeholder}</div>
  ),
}));

describe("ProviderFundingFields", () => {
  test("only captures lien-owned associations", () => {
    render(
      <ProviderFundingFields
        value={{
          medicalProviderId: "",
          facilityId: "",
          fundingCompanyId: "",
          fundingCompanyContactId: "",
        }}
        onChange={() => undefined}
      />,
    );

    expect(screen.getByText("Medical Provider")).toBeInTheDocument();
    expect(screen.getByText("Medical Facility")).toBeInTheDocument();
    expect(screen.getByText("Funding Company")).toBeInTheDocument();
    expect(screen.getByText("Contact Person")).toBeInTheDocument();
    expect(screen.queryByText("Handling Law Firm")).not.toBeInTheDocument();
    expect(screen.queryByText("Case Manager")).not.toBeInTheDocument();
  });
});
