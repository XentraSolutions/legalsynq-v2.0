import { describe, expect, it, vi } from "vitest";
import {
  buildPublicBuyerPortalActionUrl,
  buildPublicBuyerPortalGatewayRewriteUrl,
  submitPublicBuyerPortalResponse,
} from "./public-buyer-portal-actions";

describe("public buyer portal response actions", () => {
  it("builds the public response endpoint URL through the BFF", () => {
    expect(buildPublicBuyerPortalActionUrl("token-abc", "accept")).toBe(
      "/api/lien/api/liens/selling/public/token-abc/accept",
    );
    expect(buildPublicBuyerPortalActionUrl("token with space", "decline")).toBe(
      "/api/lien/api/liens/selling/public/token%20with%20space/decline",
    );
    expect(buildPublicBuyerPortalGatewayRewriteUrl("token-abc", "accept")).toBe(
      "/api/liens/api/liens/selling/public/token-abc/accept",
    );
  });

  it("posts an accept response with an idempotency key", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          accessLink: {
            createdAtUtc: "2026-07-23T13:59:57Z",
            expiresAtUtc: "2026-08-22T13:59:57Z",
            lastAccessedAtUtc: "2026-07-23T14:10:00Z",
            notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
            responseStatus: "Accepted",
            responseAmount: 21000,
            responseNotes: null,
            respondedAtUtc: "2026-07-23T14:10:00Z",
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
            notes: null,
          },
          seller: { name: "Seller", company: "Seller Co", email: "seller@example.test" },
          buyer: { contactName: "Buyer", company: "Buyer Co", email: "buyer@example.test" },
          case: { handlingLawFirm: "Firm", caseManager: null },
          documents: [],
        }),
        {
          status: 200,
          headers: { "x-correlation-id": "corr-accept" },
        },
      ),
    ) as unknown as typeof fetch;

    const result = await submitPublicBuyerPortalResponse("token-abc", "accept", {
      fetchImpl: fetchMock,
      idempotencyKey: "idem-123",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/public/token-abc/accept",
      {
        method: "POST",
        headers: {
          accept: "application/json",
          "content-type": "application/json",
          "Idempotency-Key": "idem-123",
        },
        body: "{}",
        cache: "no-store",
      },
    );
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.correlationId).toBe("corr-accept");
      expect(result.data.accessLink.responseStatus).toBe("Accepted");
    }
  });

  it("retries through the gateway rewrite path when the BFF path returns a route miss", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response("", { status: 404 }))
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            accessLink: {
              createdAtUtc: "2026-07-23T13:59:57Z",
              expiresAtUtc: "2026-08-22T13:59:57Z",
              lastAccessedAtUtc: "2026-07-23T14:10:00Z",
              notificationSubmittedAtUtc: "2026-07-23T13:59:58Z",
              responseStatus: "Accepted",
              responseAmount: 21000,
              responseNotes: null,
              respondedAtUtc: "2026-07-23T14:10:00Z",
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
              notes: null,
            },
            seller: { name: "Seller", company: "Seller Co", email: "seller@example.test" },
            buyer: { contactName: "Buyer", company: "Buyer Co", email: "buyer@example.test" },
            case: { handlingLawFirm: "Firm", caseManager: null },
            documents: [],
          }),
          { status: 200 },
        ),
      ) as unknown as typeof fetch;

    const result = await submitPublicBuyerPortalResponse("token-abc", "accept", {
      fetchImpl: fetchMock,
      idempotencyKey: "idem-123",
    });

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      "/api/lien/api/liens/selling/public/token-abc/accept",
      expect.any(Object),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "/api/liens/api/liens/selling/public/token-abc/accept",
      expect.any(Object),
    );
    expect(result.ok).toBe(true);
  });

  it("does not retry known token validation misses", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          error: {
            code: "not-found",
            title: "Lien offer link unavailable",
            message: "The secure link could not be found.",
          },
        }),
        { status: 404 },
      ),
    ) as unknown as typeof fetch;

    const result = await submitPublicBuyerPortalResponse("missing-token", "accept", {
      fetchImpl: fetchMock,
      idempotencyKey: "idem-123",
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe("not-found");
    }
  });

  it("normalizes network failures", async () => {
    const result = await submitPublicBuyerPortalResponse("token-abc", "decline", {
      fetchImpl: vi.fn().mockRejectedValue(new Error("offline")) as unknown as typeof fetch,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.status).toBe(0);
      expect(result.error.code).toBe("network-error");
      expect(result.error.message).toBe("The lien offer response could not be recorded.");
    }
  });
});
