import { describe, expect, it, vi } from "vitest";
import {
  activatePublicBuyerPortalAccount,
  buildPublicBuyerPortalActivationUrl,
} from "./public-buyer-portal-activation";

describe("public buyer portal account activation", () => {
  it("builds the BFF activation URL", () => {
    expect(buildPublicBuyerPortalActivationUrl("token-abc")).toBe(
      "/api/lien/api/liens/selling/public/token-abc/activate-account",
    );
    expect(buildPublicBuyerPortalActivationUrl("token with space")).toBe(
      "/api/lien/api/liens/selling/public/token%20with%20space/activate-account",
    );
  });

  it("posts activation through the BFF and preserves the idempotency key", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          userId: "user-123",
          isNew: true,
          loginUrl: "/login?returnTo=%2Ffunding%2Foffered-liens",
        }),
        {
          status: 200,
          headers: {
            "x-correlation-id": "corr-activation",
            "content-type": "application/json",
          },
        },
      ),
    );

    const result = await activatePublicBuyerPortalAccount(
      "token-abc",
      {
        companyName: "Capital Fund LLC",
        email: "buyer@capital.test",
        firstName: "Buyer",
        lastName: "Reviewer",
        phone: "3105551212",
        password: "Password123!",
      },
      { fetchImpl, idempotencyKey: "activate-idem-1" },
    );

    expect(fetchImpl).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/public/token-abc/activate-account",
      expect.objectContaining({
        method: "POST",
        headers: {
          accept: "application/json",
          "content-type": "application/json",
          "Idempotency-Key": "activate-idem-1",
        },
        body: JSON.stringify({
          companyName: "Capital Fund LLC",
          email: "buyer@capital.test",
          firstName: "Buyer",
          lastName: "Reviewer",
          phone: "3105551212",
          password: "Password123!",
        }),
        cache: "no-store",
      }),
    );
    expect(result).toEqual({
      ok: true,
      status: 200,
      correlationId: "corr-activation",
      data: {
        userId: "user-123",
        isNew: true,
        loginUrl: "/login?returnTo=%2Ffunding%2Foffered-liens",
      },
    });
  });

  it("normalizes API errors", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          error: {
            code: "account-conflict",
            title: "Account activation failed",
            message: "Use your existing password.",
          },
        }),
        { status: 409 },
      ),
    );

    const result = await activatePublicBuyerPortalAccount(
      "token-abc",
      {
        firstName: "Buyer",
        password: "Password123!",
      },
      { fetchImpl },
    );

    expect(result).toMatchObject({
      ok: false,
      status: 409,
      error: {
        code: "account-conflict",
        message: "Use your existing password.",
      },
    });
  });
});
