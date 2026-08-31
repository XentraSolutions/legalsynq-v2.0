import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";
import PublicBuyerPortalPage from "./page";
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

const buyerPortalData: PublicBuyerPortalData = {
  audience: "buyer",
  accessLink: {
    createdAtUtc: "2026-07-23T13:59:57.67655Z",
    expiresAtUtc: "2026-08-22T13:59:57.67655Z",
    lastAccessedAtUtc: null,
    notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
    responseStatus: null,
    responseAmount: null,
    responseNotes: null,
    respondedAtUtc: null,
  },
  lien: {
    id: "019f8a97-aa3c-7fe0-aa87-e3b8c693f96b",
    lienCode: "LIEN-CONF-20260722161022",
    status: "Offered",
    sellerStatus: "SubmittedForSale",
    submittedAtUtc: "2026-07-22T16:10:23.33274Z",
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
    email: "ralph.lopez+1@xentragroup.com",
  },
  buyer: {
    contactName: "Ralph Buyer",
    company: "Xentra Group Funding Review",
    email: "ralph.lopez+200@xentragroup.com",
    phone: "3105551212",
  },
  case: {
    handlingLawFirm: "Anderson & Ashworth Law Firm LLC",
    handlingLawFirmContactName: "Anderson Contact",
    handlingLawFirmEmail: "anderson.contact@ashworthlaw.test",
    caseManager: "Case Manager",
  },
  documents: [
    {
      id: "019f8a97-aa3d-70ef-a549-a7710b38d4b5",
      fileName: "signed-lien-real.pdf",
      category: "Lien Document",
      sizeOrType: "PDF",
      viewUrl:
        "/api/lien/api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/view",
      downloadUrl:
        "/api/lien/api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/download",
    },
  ],
};

function makeBuyerPortalData(
  overrides: Partial<PublicBuyerPortalData> = {},
): PublicBuyerPortalData {
  return {
    ...buyerPortalData,
    ...overrides,
    accessLink: {
      ...buyerPortalData.accessLink,
      ...(overrides.accessLink ?? {}),
    },
    lien: {
      ...buyerPortalData.lien,
      ...(overrides.lien ?? {}),
    },
    seller: {
      ...buyerPortalData.seller,
      ...(overrides.seller ?? {}),
    },
    buyer: {
      ...buyerPortalData.buyer,
      ...(overrides.buyer ?? {}),
    },
    case: {
      ...buyerPortalData.case,
      ...(overrides.case ?? {}),
    },
    documents: overrides.documents ?? buyerPortalData.documents,
    messages: overrides.messages ?? buyerPortalData.messages,
  };
}

describe("PublicBuyerPortalPage", () => {
  beforeEach(() => {
    fetchPublicBuyerPortalMock.mockReset();
  });

  test("renders the temporary buyer portal from Liens JSON data", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-123",
      data: makeBuyerPortalData(),
    });

    const page = await PublicBuyerPortalPage({
      params: Promise.resolve({ token: "token-abc" }),
    });
    render(page);

    expect(fetchPublicBuyerPortalMock).toHaveBeenCalledWith("token-abc", {
      requestHost: "synqlien-demo.localhost:3000",
      requestProto: "http",
    });
    expect(screen.getByText("LEGALSYNQ")).toBeInTheDocument();
    expect(screen.getByText("Funding Company Portal")).toBeInTheDocument();
    expect(document.querySelector('img[src="/figma/synqlien-funding-public/icon-logo.svg"]')).toHaveAttribute(
      "src",
      "/figma/synqlien-funding-public/icon-logo.svg",
    );
    expect(screen.getByText("Manage Offered Liens")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Activate Free Account" })).toHaveAttribute(
      "href",
      "/selling/public/token-abc/activate",
    );
    expect(screen.getByText("Your Response")).toBeInTheDocument();
    expect(screen.getByText("Lien Summary")).toBeInTheDocument();
    expect(screen.getByText("Awaiting Your Response")).toBeInTheDocument();
    expect(screen.queryByText("Listing Visibility")).not.toBeInTheDocument();
    expect(screen.queryByText("Private")).not.toBeInTheDocument();
    expect(screen.getByText("Seller Information")).toBeInTheDocument();
    expect(screen.getAllByText("RL Liens1").length).toBeGreaterThan(0);
    expect(screen.getByText("Funding Company & Case Information")).toBeInTheDocument();
    expect(screen.getByText("Anderson & Ashworth Law Firm LLC")).toBeInTheDocument();
    expect(screen.getByText("Anderson Contact")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "anderson.contact@ashworthlaw.test" })).toHaveAttribute(
      "href",
      "mailto:anderson.contact@ashworthlaw.test",
    );
    expect(screen.getByText("signed-lien-real.pdf")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View signed-lien-real.pdf" })).toHaveAttribute(
      "href",
      "/api/lien/api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/view",
    );
    expect(screen.getByRole("link", { name: "View signed-lien-real.pdf" })).toHaveAttribute(
      "target",
      "_blank",
    );
    expect(screen.getByRole("link", { name: "View signed-lien-real.pdf" })).toHaveAttribute(
      "rel",
      "noopener noreferrer",
    );
    const downloadLink = screen.getByRole("link", { name: "Download signed-lien-real.pdf" });
    expect(downloadLink).toHaveAttribute(
      "href",
      "/api/lien/api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/download",
    );
    expect(downloadLink).not.toHaveAttribute("target");
    expect(screen.queryByText("John Doe")).not.toBeInTheDocument();
    expect(screen.queryByText("Velantrix")).not.toBeInTheDocument();
  });

  test("renders a SynqLien login CTA when the buyer account already exists", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-existing-account",
      data: makeBuyerPortalData({
        account: {
          hasExistingAccount: true,
          loginUrl: "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation",
        },
      }),
    });

    const page = await PublicBuyerPortalPage({
      params: Promise.resolve({ token: "token-abc" }),
    });
    render(page);

    expect(screen.getByRole("link", { name: "Log In" })).toHaveAttribute(
      "href",
      "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation",
    );
    expect(screen.queryByRole("link", { name: "Activate Free Account" })).not.toBeInTheDocument();
  });

  test("renders seller public links as read-only without activation or response actions", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-seller",
      data: {
        audience: "seller",
        accessLink: {
          createdAtUtc: "2026-07-23T13:59:57.67655Z",
          expiresAtUtc: "2026-08-22T13:59:57.67655Z",
          lastAccessedAtUtc: null,
          notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
          responseStatus: null,
          responseAmount: null,
          responseNotes: null,
          respondedAtUtc: null,
        },
        lien: {
          id: "019f8a97-aa3c-7fe0-aa87-e3b8c693f96b",
          lienCode: "LIEN-CONF-20260722161022",
          status: "Offered",
          sellerStatus: "SubmittedForSale",
          submittedAtUtc: "2026-07-22T16:10:23.33274Z",
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
          email: "seller@example.test",
        },
        buyer: {
          contactName: "Buyer Reviewer",
          company: "Capital Fund LLC",
          email: "buyer@example.test",
          phone: "3105551212",
        },
        case: {
          handlingLawFirm: "Smith & Associates LLP",
          handlingLawFirmContactName: "Sarah Mitchell",
          handlingLawFirmEmail: "s.mitchell@crestfield.com",
          caseManager: "Case Manager",
        },
        documents: [],
      },
    });

    const page = await PublicBuyerPortalPage({
      params: Promise.resolve({ token: "seller-token" }),
    });
    render(page);

    expect(screen.getByText("View Offered Liens")).toBeInTheDocument();
    expect(screen.getByText("Offered")).toBeInTheDocument();
    expect(screen.queryByText("Lien Details Sent")).not.toBeInTheDocument();
    expect(screen.queryByText(/This lien offer was sent to/)).not.toBeInTheDocument();
    expect(screen.queryByText("Sent to Funding Company")).not.toBeInTheDocument();
    expect(screen.queryByText("Listing Visibility")).not.toBeInTheDocument();
    expect(screen.queryByText("Private")).not.toBeInTheDocument();
    expect(screen.getByText("Buyer Information")).toBeInTheDocument();
    expect(screen.getByText("Buyer Reviewer")).toBeInTheDocument();
    expect(screen.getAllByText("Capital Fund LLC").length).toBeGreaterThan(0);
    expect(screen.getByText("Case Information")).toBeInTheDocument();
    expect(screen.getByText("Smith & Associates LLP")).toBeInTheDocument();
    expect(screen.getByText("Sarah Mitchell")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "s.mitchell@crestfield.com" })).toHaveAttribute(
      "href",
      "mailto:s.mitchell@crestfield.com",
    );
    expect(screen.queryByRole("link", { name: "Activate Free Account" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept Lien" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Decline Lien" })).not.toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Message" })).toBeInTheDocument();
    expect(screen.getByText("No messages yet. Send a message to the buyer below.")).toBeInTheDocument();
  });

  test("renders the public link state when Liens returns an error", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: false,
      status: 410,
      correlationId: null,
      error: {
        code: "expired",
        title: "Lien offer link expired",
        message: "This secure link has expired.",
      },
    });

    const page = await PublicBuyerPortalPage({
      params: Promise.resolve({ token: "expired-token" }),
    });
    render(page);

    expect(screen.getByText("Lien offer link expired")).toBeInTheDocument();
    expect(screen.getByText("This secure link has expired.")).toBeInTheDocument();
  });
});
