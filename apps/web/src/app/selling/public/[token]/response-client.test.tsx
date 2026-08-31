import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";
import type { PublicBuyerPortalData } from "@/lib/liens/public-buyer-portal";
import { submitPublicBuyerPortalResponse } from "@/lib/liens/public-buyer-portal-actions";
import { PublicBuyerPortalInteractiveContent } from "./response-client";

vi.mock("@/lib/liens/public-buyer-portal-actions", () => ({
  submitPublicBuyerPortalResponse: vi.fn(),
}));

const submitPublicBuyerPortalResponseMock = vi.mocked(submitPublicBuyerPortalResponse);

describe("PublicBuyerPortalInteractiveContent", () => {
  beforeEach(() => {
    submitPublicBuyerPortalResponseMock.mockReset();
  });

  test("records an accepted response and disables both response buttons", async () => {
    submitPublicBuyerPortalResponseMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-accept",
      data: withResponse(basePortalData(), {
        responseStatus: "Accepted",
        responseAmount: 21000,
        respondedAtUtc: "2026-07-23T14:10:00Z",
      }),
    });

    render(<PublicBuyerPortalInteractiveContent token="token-abc" data={basePortalData()} />);

    await userEvent.click(screen.getByRole("button", { name: "Accept Lien" }));

    await waitFor(() => {
      expect(submitPublicBuyerPortalResponseMock).toHaveBeenCalledWith("token-abc", "accept");
    });
    expect(screen.getByRole("button", { name: "Accepted" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Decline Lien" })).toBeDisabled();
    expect(screen.getAllByText("Accepted").length).toBeGreaterThan(0);
    expect(screen.getByText(/Your response was securely recorded/)).toBeInTheDocument();
  });

  test("records a declined response and updates the badge", async () => {
    submitPublicBuyerPortalResponseMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-decline",
      data: withResponse(basePortalData(), {
        responseStatus: "Declined",
        responseNotes: "Not in buying criteria",
        respondedAtUtc: "2026-07-23T14:11:00Z",
      }),
    });

    render(<PublicBuyerPortalInteractiveContent token="token-abc" data={basePortalData()} />);

    await userEvent.click(screen.getByRole("button", { name: "Decline Lien" }));

    await waitFor(() => {
      expect(submitPublicBuyerPortalResponseMock).toHaveBeenCalledWith("token-abc", "decline");
    });
    expect(screen.getByRole("button", { name: "Declined" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Accept Lien" })).toBeDisabled();
    expect(screen.getAllByText("Declined").length).toBeGreaterThan(0);
  });

  test("shows an inline error when the response cannot be recorded", async () => {
    submitPublicBuyerPortalResponseMock.mockResolvedValue({
      ok: false,
      status: 409,
      correlationId: "corr-conflict",
      error: {
        code: "response-conflict",
        title: "Lien response already recorded",
        message: "A different response has already been securely recorded for this lien offer.",
      },
    });

    render(<PublicBuyerPortalInteractiveContent token="token-abc" data={basePortalData()} />);

    await userEvent.click(screen.getByRole("button", { name: "Decline Lien" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "A different response has already been securely recorded for this lien offer.",
    );
    expect(screen.getByRole("button", { name: "Accept Lien" })).not.toBeDisabled();
    expect(screen.getByRole("button", { name: "Decline Lien" })).not.toBeDisabled();
  });

  test("renders seller email address on the buyer-facing lien summary", () => {
    render(<PublicBuyerPortalInteractiveContent token="token-abc" data={basePortalData()} />);

    expect(screen.getByText("Seller Information")).toBeInTheDocument();
    expect(screen.queryByText("Listing Visibility")).not.toBeInTheDocument();
    expect(screen.queryByText("Private")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "seller@example.test" })).toHaveAttribute(
      "href",
      "mailto:seller@example.test",
    );
  });

  test("renders law firm contact details on the buyer-facing funding case information", () => {
    render(<PublicBuyerPortalInteractiveContent token="token-abc" data={basePortalData()} />);

    expect(screen.getByText("Funding Company & Case Information")).toBeInTheDocument();
    expect(screen.getByText("Anderson Contact")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "anderson.contact@ashworthlaw.test" })).toHaveAttribute(
      "href",
      "mailto:anderson.contact@ashworthlaw.test",
    );
  });

  test("renders seller audience as a read-only view with buyer information", () => {
    render(
      <PublicBuyerPortalInteractiveContent
        token="token-abc"
        data={{ ...basePortalData(), audience: "seller" }}
      />,
    );

    expect(screen.getByText("Offered")).toBeInTheDocument();
    expect(screen.queryByText("Sent to Funding Company")).not.toBeInTheDocument();
    expect(screen.queryByText(/This lien offer was sent to/)).not.toBeInTheDocument();
    expect(screen.queryByText("Listing Visibility")).not.toBeInTheDocument();
    expect(screen.queryByText("Private")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept Lien" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Decline Lien" })).not.toBeInTheDocument();
    expect(screen.getByText("Buyer Information")).toBeInTheDocument();
    expect(screen.getByText("Ralph Buyer")).toBeInTheDocument();
    expect(screen.getAllByText("Xentra Group Funding Review").length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "buyer@example.test" })).toHaveAttribute(
      "href",
      "mailto:buyer@example.test",
    );
    expect(screen.getByText("Case Information")).toBeInTheDocument();
    expect(screen.getByText("Anderson Contact")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "anderson.contact@ashworthlaw.test" })).toHaveAttribute(
      "href",
      "mailto:anderson.contact@ashworthlaw.test",
    );
    expect(submitPublicBuyerPortalResponseMock).not.toHaveBeenCalled();
  });

  test("shows mirrored buyer response status on seller read-only view", () => {
    const data = basePortalData();

    render(
      <PublicBuyerPortalInteractiveContent
        token="token-abc"
        data={{
          ...data,
          audience: "seller",
          accessLink: {
            ...data.accessLink,
            responseStatus: "Declined",
            responseNotes: "Not in buying criteria",
            respondedAtUtc: "2026-07-23T14:11:00Z",
          },
        }}
      />,
    );

    expect(screen.getByText("Declined")).toBeInTheDocument();
    expect(screen.queryByText("Offered")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept Lien" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Decline Lien" })).not.toBeInTheDocument();
  });
});

function basePortalData(): PublicBuyerPortalData {
  return {
    audience: "buyer",
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
      name: "RL Liens1",
      company: "RL Liens1",
      email: "seller@example.test",
    },
    buyer: {
      contactName: "Ralph Buyer",
      company: "Xentra Group Funding Review",
      email: "buyer@example.test",
      phone: "3105551212",
    },
    case: {
      handlingLawFirm: "Anderson & Ashworth Law Firm LLC",
      handlingLawFirmContactName: "Anderson Contact",
      handlingLawFirmEmail: "anderson.contact@ashworthlaw.test",
      caseManager: "Case Manager",
    },
    documents: [],
  };
}

function withResponse(
  data: PublicBuyerPortalData,
  response: Partial<PublicBuyerPortalData["accessLink"]>,
): PublicBuyerPortalData {
  const responseStatus = response.responseStatus === "Accepted" || response.responseStatus === "Declined"
    ? response.responseStatus
    : null;
  const lienStatus = responseStatus ?? data.lien.status;
  const sellerStatus = responseStatus ?? data.lien.sellerStatus;

  return {
    ...data,
    accessLink: {
      ...data.accessLink,
      ...response,
    },
    lien: {
      ...data.lien,
      status: lienStatus,
      sellerStatus,
    },
  };
}
