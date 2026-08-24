import { NextRequest } from "next/server";
import { cookies } from "next/headers";
import { beforeEach, describe, expect, test, vi } from "vitest";

vi.mock("next/headers", () => ({
  cookies: vi.fn(),
}));

type RouteContext = { params: Promise<{ path: string[] }> };

function makeRequest(
  path: string,
  options: { method?: string; body?: object; idempotencyKey?: string } = {},
): [NextRequest, RouteContext] {
  const headers: Record<string, string> = {
    "content-type": "application/json",
  };
  if (options.idempotencyKey) {
    headers["Idempotency-Key"] = options.idempotencyKey;
  }

  const request = new NextRequest(`http://localhost/api/selling/${path}`, {
    method: options.method ?? "POST",
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });
  return [request, { params: Promise.resolve({ path: path.split("/") }) }];
}

describe("SynqLien catch-all proxy", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue(undefined),
    } as never);
  });

  test("forwards public response posts without auth and preserves idempotency key", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
          "X-Correlation-Id": "corr-public-response",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { POST } = await import("./route");
    const [req, ctx] = makeRequest(
      "api/liens/selling/public/token-abc/accept",
      {
        body: {},
        idempotencyKey: "idem-123",
      },
    );

    const response = await POST(req, ctx);

    expect(fetchSpy).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/liens/api/liens/selling/public/token-abc/accept",
      {
        method: "POST",
        headers: {
          "Idempotency-Key": "idem-123",
          "Content-Type": "application/json",
        },
        body: "{}",
      },
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("X-Correlation-Id")).toBe(
      "corr-public-response",
    );
  });
});
