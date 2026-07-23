import { describe, expect, it, vi } from "vitest";
import {
  buildPublicBuyerPortalGatewayUrl,
  fetchPublicBuyerPortal,
} from "./public-buyer-portal";

describe("public buyer portal data fetch", () => {
  const expectedPublicEndpoint =
    `${(process.env.GATEWAY_URL ?? "http://127.0.0.1:5010").replace(/\/+$/, "")}` +
    "/liens/api/liens/selling/public/token-abc";

  it("builds the Liens public JSON endpoint URL through the gateway", () => {
    expect(buildPublicBuyerPortalGatewayUrl("token-abc")).toBe(expectedPublicEndpoint);
  });

  it("fetches public buyer portal JSON with request origin metadata", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          accessLink: {
            createdAtUtc: "2026-07-23T13:59:57.67655Z",
            expiresAtUtc: "2026-08-22T13:59:57.67655Z",
            lastAccessedAtUtc: null,
            notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
          },
          lien: {
            id: "lien-123",
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
            notes: "Real lien notes",
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
            caseManager: null,
          },
          documents: [],
        }),
        {
          status: 200,
          headers: {
            "content-type": "application/json; charset=utf-8",
            "x-correlation-id": "corr-123",
          },
        },
      ),
    ) as unknown as typeof fetch;

    const result = await fetchPublicBuyerPortal("token-abc", {
      fetchImpl: fetchMock,
      requestHost: "synqlien-demo.localhost:3000",
      requestProto: "http",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      expectedPublicEndpoint,
      {
        method: "GET",
        headers: {
          accept: "application/json",
          "x-legal-synq-public-host": "synqlien-demo.localhost:3000",
          "x-legal-synq-public-proto": "http",
        },
        cache: "no-store",
      },
    );
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.correlationId).toBe("corr-123");
      expect(result.data.lien.lienCode).toBe("LIEN-CONF-20260722161022");
      expect(result.data.seller.name).toBe("RL Liens1");
    }
  });

  it("normalizes public link error responses", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          error: {
            code: "expired",
            title: "Lien offer link expired",
            message: "This secure link has expired.",
          },
        }),
        {
          status: 410,
          headers: { "content-type": "application/json; charset=utf-8" },
        },
      ),
    ) as unknown as typeof fetch;

    const result = await fetchPublicBuyerPortal("expired-token", {
      fetchImpl: fetchMock,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.status).toBe(410);
      expect(result.error.code).toBe("expired");
      expect(result.error.title).toBe("Lien offer link expired");
    }
  });
});
