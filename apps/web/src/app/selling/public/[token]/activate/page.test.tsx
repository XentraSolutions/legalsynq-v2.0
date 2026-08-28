import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";
import PublicBuyerActivationPage from "./page";
import {
  fetchPublicBuyerPortal,
  type PublicBuyerPortalData,
} from "@/lib/liens/public-buyer-portal";

vi.mock("next/headers", () => ({
  headers: vi.fn(async () => new Headers({
    host: "synqlien-demo.localhost:3000",
    "x-forwarded-proto": "http",
  })),
}));

vi.mock("@/lib/liens/public-buyer-portal", () => ({
  fetchPublicBuyerPortal: vi.fn(),
  SYNQLIEN_BUYER_LOGIN_URL:
    "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation",
}));

const fetchPublicBuyerPortalMock = vi.mocked(fetchPublicBuyerPortal);

describe("PublicBuyerActivationPage", () => {
  beforeEach(() => {
    fetchPublicBuyerPortalMock.mockReset();
  });

  test("renders the intro screen and links Get Started to registration", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-activate-intro",
      data: basePortalData(),
    });

    const page = await PublicBuyerActivationPage({
      params: Promise.resolve({ token: "token-abc" }),
    });
    render(page);

    expect(screen.getByText("Review, and manage liens in one place")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Get Started" })).toHaveAttribute(
      "href",
      "/selling/public/token-abc/activate/register",
    );
    expect(screen.getByAltText("LegalSynq")).toHaveAttribute(
      "src",
      "/figma/synqlien-funding-intro/legalsynq-logo.svg",
    );
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/arrow-left.svg"]')).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/arrow-right.svg"]')).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/file-input.svg"]')).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/layout-dashboard.svg"]')).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/bell-ring.svg"]')).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-intro/receipt-text.svg"]')).toBeInTheDocument();
    expect(screen.queryByText("Create Portal Login")).not.toBeInTheDocument();
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
