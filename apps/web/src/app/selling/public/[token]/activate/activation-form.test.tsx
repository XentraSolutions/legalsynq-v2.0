import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";
import type { PublicBuyerPortalData } from "@/lib/liens/public-buyer-portal";
import { activatePublicBuyerPortalAccount } from "@/lib/liens/public-buyer-portal-activation";
import { PublicBuyerActivationForm } from "./activation-form";

vi.mock("@/lib/liens/public-buyer-portal-activation", () => ({
  activatePublicBuyerPortalAccount: vi.fn(),
}));

const activatePublicBuyerPortalAccountMock = vi.mocked(activatePublicBuyerPortalAccount);

describe("PublicBuyerActivationForm", () => {
  beforeEach(() => {
    activatePublicBuyerPortalAccountMock.mockReset();
  });

  test("locks prefilled buyer details and submits activation through the BFF helper", async () => {
    activatePublicBuyerPortalAccountMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-activate",
      data: {
        userId: "user-123",
        isNew: true,
        loginUrl: "/login?returnTo=%2Ffunding%2Foffered-liens",
      },
    });

    render(<PublicBuyerActivationForm token="token-abc" data={basePortalData()} />);

    expect(screen.getByLabelText(/Company Name/i)).toBeDisabled();
    expect(screen.getByLabelText(/Email Address/i)).toBeDisabled();
    expect(screen.getByLabelText(/First Name/i)).toBeDisabled();
    expect(screen.getByLabelText(/Last Name/i)).toBeDisabled();
    expect(screen.getByLabelText(/Phone Number/i)).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/^Password/i), "Password123!");
    await userEvent.type(
      screen.getByLabelText(/Confirm Password/i, { selector: "input" }),
      "Password123!",
    );
    await userEvent.click(screen.getByLabelText(/I agree/i));
    await userEvent.click(screen.getByRole("button", { name: /Activate Free Account/i }));

    await waitFor(() => {
      expect(activatePublicBuyerPortalAccountMock).toHaveBeenCalledWith(
        "token-abc",
        {
          companyName: "Capital Fund LLC",
          email: "buyer.portal@capital.test",
          firstName: "Buyer",
          lastName: "Reviewer",
          phone: "+13105551212",
          password: "Password123!",
        },
      );
    });
    expect(await screen.findByText("Account activated")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Log in to Manage Liens" })).toHaveAttribute(
      "href",
      "/login?returnTo=%2Ffunding%2Foffered-liens",
    );
  });

  test("shows inline activation errors", async () => {
    activatePublicBuyerPortalAccountMock.mockResolvedValue({
      ok: false,
      status: 409,
      correlationId: "corr-conflict",
      error: {
        code: "account-conflict",
        title: "Account activation failed",
        message: "Use your existing password.",
      },
    });

    render(<PublicBuyerActivationForm token="token-abc" data={basePortalData()} />);

    await userEvent.type(screen.getByLabelText(/^Password/i), "Password123!");
    await userEvent.type(
      screen.getByLabelText(/Confirm Password/i, { selector: "input" }),
      "Password123!",
    );
    await userEvent.click(screen.getByLabelText(/I agree/i));
    await userEvent.click(screen.getByRole("button", { name: /Activate Free Account/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Use your existing password.");
  });
});

function basePortalData(): PublicBuyerPortalData {
  return {
    accessLink: {
      createdAtUtc: "2026-07-23T13:59:57Z",
      expiresAtUtc: "2026-08-22T13:59:57Z",
      lastAccessedAtUtc: null,
      notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
      responseStatus: null,
      responseAmount: null,
      responseNotes: null,
      respondedAtUtc: null,
    },
    lien: {
      id: "lien-123",
      lienCode: "LIEN-123",
      status: "Offered",
      sellerStatus: "SubmittedForSale",
      submittedAtUtc: "2026-07-22T16:10:23Z",
      listingVisibility: "Private",
      initialServiceDate: "2026-01-12",
      endServiceDate: "2026-02-14",
      originalAmount: 24850,
      askAmount: 21000,
      offerPrice: 21000,
      notes: "Medical provider lien filed after treatment and pending review.",
    },
    seller: {
      name: "Seller Operator",
      company: "Smith & Associates LLP",
      email: "seller.portal@smithlaw.test",
    },
    buyer: {
      contactName: "Buyer Reviewer",
      company: "Capital Fund LLC",
      email: "buyer.portal@capital.test",
      phone: "(310) 555-1212",
    },
    case: {
      handlingLawFirm: "Smith & Associates LLP",
      caseManager: "Case Manager",
    },
    documents: [],
  };
}
