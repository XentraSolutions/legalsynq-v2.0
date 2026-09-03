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
        redirect: "manual",
      },
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("X-Correlation-Id")).toBe(
      "corr-public-response",
    );
  });

  test("preserves seller message attachment redirects through the document access proxy", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn((name: string) =>
        name === "platform_session" ? { value: "session-token" } : undefined,
      ),
    } as never);
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 302,
        headers: {
          Location: "/documents/access/attachment-token",
          "X-Correlation-Id": "corr-message-attachment",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest(
      "api/liens/selling/liens/lien-1/message-attachments/attachment-1/view",
      { method: "GET" },
    );

    const response = await GET(req, ctx);

    expect(fetchSpy).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/liens/api/liens/selling/liens/lien-1/message-attachments/attachment-1/view",
      expect.objectContaining({
        method: "GET",
        headers: {
          Authorization: "Bearer session-token",
        },
        redirect: "manual",
      }),
    );
    expect(response.status).toBe(302);
    expect(response.headers.get("Location")).toBe("/api/lien/documents/access/attachment-token");
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-message-attachment");
  });

  test("adds the assumed 'keep' action to a lien detail response when prepare-sale is allowed", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({ lienId: "lien-1", availableActions: ["prepare-sale", "archive"] }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("api/liens/selling/liens/lien-1", {
      method: "GET",
    });

    const response = await GET(req, ctx);
    const body = await response.json();

    expect(body.availableActions).toEqual(["prepare-sale", "archive", "keep"]);
  });

  test("leaves availableActions untouched on the lien detail response when prepare-sale isn't allowed", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({ lienId: "lien-1", availableActions: ["archive"] }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("api/liens/selling/liens/lien-1", {
      method: "GET",
    });

    const response = await GET(req, ctx);
    const body = await response.json();

    expect(body.availableActions).toEqual(["archive"]);
  });

  test("adds the assumed 'keep' action to each item in a lien list response", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          items: [
            { lienId: "lien-1", availableActions: ["prepare-sale"] },
            { lienId: "lien-2", availableActions: ["confirm-sale"] },
          ],
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("api/liens/selling/liens", {
      method: "GET",
    });

    const response = await GET(req, ctx);
    const body = await response.json();

    expect(body.items[0].availableActions).toEqual(["prepare-sale", "keep"]);
    expect(body.items[1].availableActions).toEqual(["confirm-sale"]);
  });

  test("does not touch responses from subpaths like lien activity", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({ availableActions: ["prepare-sale"] }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("api/liens/selling/liens/lien-1/activity", {
      method: "GET",
    });

    const response = await GET(req, ctx);
    const body = await response.json();

    expect(body.availableActions).toEqual(["prepare-sale"]);
  });
});
