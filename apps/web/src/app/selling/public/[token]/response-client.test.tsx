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
      name: "RL Liens1",
      company: "RL Liens1",
      email: "seller@example.test",
    },
    buyer: {
      contactName: "Ralph Buyer",
      company: "Xentra Group Funding Review",
      email: "buyer@example.test",
    },
    case: {
      handlingLawFirm: "RL Liens1",
      caseManager: "Case Manager",
    },
    documents: [],
  };
}

function withResponse(
  data: PublicBuyerPortalData,
  response: Partial<PublicBuyerPortalData["accessLink"]>,
): PublicBuyerPortalData {
  return {
    ...data,
    accessLink: {
      ...data.accessLink,
      ...response,
    },
  };
}
