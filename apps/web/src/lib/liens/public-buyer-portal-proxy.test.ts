import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { proxyPublicBuyerPortal } from "./public-buyer-portal-proxy";

describe("proxyPublicBuyerPortal", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("forwards clean public buyer portal URLs to the Liens public endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response("<!doctype html><html><body>Manage Offered Liens</body></html>", {
        status: 200,
        headers: {
          "content-type": "text/html; charset=utf-8",
          "x-correlation-id": "corr-123",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const response = await proxyPublicBuyerPortal(
      new NextRequest(
        "https://tenant.legalsynq.test/selling/public/token-abc?from=email",
        { headers: { accept: "text/html" } },
      ),
      "token-abc",
    );

    expect(fetchMock).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/liens/api/liens/selling/public/token-abc?from=email",
      {
        method: "GET",
        headers: {
          accept: "text/html",
          "x-legal-synq-public-host": "tenant.legalsynq.test",
          "x-legal-synq-public-proto": "https",
        },
        cache: "no-store",
      },
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toBe(
      "text/html; charset=utf-8",
    );
    expect(response.headers.get("x-correlation-id")).toBe("corr-123");
    await expect(response.text()).resolves.toContain("Manage Offered Liens");
  });
});
