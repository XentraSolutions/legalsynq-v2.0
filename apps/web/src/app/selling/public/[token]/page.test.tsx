import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";
import PublicBuyerPortalPage from "./page";
import { fetchPublicBuyerPortal } from "@/lib/liens/public-buyer-portal";

vi.mock("next/headers", () => ({
  headers: vi.fn(async () => new Headers({
    host: "synqlien-demo.localhost:3000",
    "x-forwarded-proto": "http",
  })),
}));

vi.mock("@/lib/liens/public-buyer-portal", () => ({
  fetchPublicBuyerPortal: vi.fn(),
}));

const fetchPublicBuyerPortalMock = vi.mocked(fetchPublicBuyerPortal);

describe("PublicBuyerPortalPage", () => {
  beforeEach(() => {
    fetchPublicBuyerPortalMock.mockReset();
  });

  test("renders the temporary buyer portal from Liens JSON data", async () => {
    fetchPublicBuyerPortalMock.mockResolvedValue({
      ok: true,
      status: 200,
      correlationId: "corr-123",
      data: {
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
        },
        case: {
          handlingLawFirm: "RL Liens1",
          caseManager: "Case Manager",
        },
        documents: [
          {
            fileName: "signed-lien-real.pdf",
            category: "Lien Document",
            sizeOrType: "PDF",
          },
        ],
      },
    });

    const page = await PublicBuyerPortalPage({
      params: Promise.resolve({ token: "token-abc" }),
    });
    render(page);

    expect(fetchPublicBuyerPortalMock).toHaveBeenCalledWith("token-abc", {
      requestHost: "synqlien-demo.localhost:3000",
      requestProto: "http",
    });
    expect(screen.getByAltText("LegalSynq")).toHaveAttribute(
      "src",
      "/legalsynq-logo-temp-portal.svg",
    );
    expect(screen.getByText("Manage Offered Liens")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Activate Free Account" })).toHaveAttribute(
      "href",
      "/selling/public/token-abc/activate",
    );
    expect(screen.getByText("Your Response")).toBeInTheDocument();
    expect(screen.getByText("Lien Summary")).toBeInTheDocument();
    expect(screen.getByText("Awaiting Your Response")).toBeInTheDocument();
    expect(screen.getByText("Seller Information")).toBeInTheDocument();
    expect(screen.getAllByText("RL Liens1").length).toBeGreaterThan(0);
    expect(screen.getByText("Funding Company & Case Information")).toBeInTheDocument();
    expect(screen.getByText("signed-lien-real.pdf")).toBeInTheDocument();
    expect(screen.queryByText("John Doe")).not.toBeInTheDocument();
    expect(screen.queryByText("Velantrix")).not.toBeInTheDocument();
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
