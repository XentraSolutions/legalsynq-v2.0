import { render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { CaseIntakeForm, PlaintiffIntakeForm } from "./case-intake-form";

vi.mock("@/providers/session-provider", () => ({
  useSessionContext: () => ({
    lookup: { CaseStatus: [], AccidentType: [], State: [] },
  }),
}));

vi.mock("@/components/lien/field", () => ({
  default: ({ label }: { label: string }) => <div>{label}</div>,
}));

vi.mock("@/components/selling/selling-entity-select", () => ({
  SellingEntitySelect: ({ placeholder }: { placeholder?: string }) => (
    <div>{placeholder}</div>
  ),
}));

describe("selling case intake", () => {
  test("renders all case-first fields before lien intake", () => {
    render(<CaseIntakeForm onFormValid={() => undefined} />);

    expect(screen.getByText("Case Status")).toBeInTheDocument();
    expect(screen.getByText("Accident Type")).toBeInTheDocument();
    expect(screen.getByText("Accident State")).toBeInTheDocument();
    expect(screen.getByText("Date of Loss")).toBeInTheDocument();
    expect(screen.getByText("Law Firm")).toBeInTheDocument();
    expect(screen.getByText("Case Manager")).toBeInTheDocument();
    expect(screen.getByText("Case Tracking Notes")).toBeInTheDocument();
  });

  test("renders plaintiff fields as the second creation step", () => {
    render(<PlaintiffIntakeForm onFormValid={() => undefined} />);

    expect(screen.getByText("First Name")).toBeInTheDocument();
    expect(screen.getByText("Last Name")).toBeInTheDocument();
    expect(screen.getByText("Birthdate")).toBeInTheDocument();
    expect(screen.getByText("Email")).toBeInTheDocument();
    expect(screen.getByText("Phone")).toBeInTheDocument();
    expect(screen.getByText("Gender")).toBeInTheDocument();
    expect(screen.getByText("Address")).toBeInTheDocument();
    expect(screen.getByText("City")).toBeInTheDocument();
    expect(screen.getByText("State")).toBeInTheDocument();
    expect(screen.getByText("Zipcode")).toBeInTheDocument();
  });
});
