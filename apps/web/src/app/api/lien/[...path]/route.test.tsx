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
    host: "synqlien-demo.localhost:3000",
    "x-forwarded-proto": "http",
  };
  if (options.idempotencyKey) {
    headers["Idempotency-Key"] = options.idempotencyKey;
  }

  const request = new NextRequest(`http://localhost/api/lien/${path}`, {
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
          "x-legal-synq-public-host": "synqlien-demo.localhost:3000",
          "x-legal-synq-public-proto": "http",
          "Content-Type": "application/json",
        },
        body: "{}",
        redirect: "manual",
      },
    );
    expect(response.status).toBe(200);
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-public-response");
  });

  test("preserves public document redirects through the document access proxy", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 302,
        headers: {
          Location: "/documents/access/abc123",
          "X-Correlation-Id": "corr-public-doc",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest(
      "api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/view",
      { method: "GET" },
    );

    const response = await GET(req, ctx);

    expect(fetchSpy).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/liens/api/liens/selling/public/token-abc/documents/019f8a97-aa3d-70ef-a549-a7710b38d4b5/view",
      expect.objectContaining({
        method: "GET",
        redirect: "manual",
      }),
    );
    expect(response.status).toBe(302);
    expect(response.headers.get("Location")).toBe("/api/lien/documents/access/abc123");
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-public-doc");
  });

  test("forwards multipart uploads with the original content type", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn((name: string) =>
        name === "platform_session" ? { value: "session-token" } : undefined,
      ),
    } as never);
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ isSuccess: true }), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
          "X-Correlation-Id": "corr-upload",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const uploadBytes = new Uint8Array([1, 2, 3, 4]);
    const request = new NextRequest(
      "http://localhost/api/lien/api/liens/cases/upload/document",
      {
        method: "POST",
        headers: {
          "content-type": "multipart/form-data; boundary=----test",
          host: "synqlien-demo.localhost:3000",
          "x-forwarded-proto": "https",
        },
        body: uploadBytes,
      },
    );

    const { POST } = await import("./route");
    const response = await POST(request, {
      params: Promise.resolve({
        path: ["api", "liens", "cases", "upload", "document"],
      }),
    });

    expect(response.status).toBe(200);
    expect(fetchSpy).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/liens/api/liens/cases/upload/document",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          Authorization: "Bearer session-token",
          "Content-Type": "multipart/form-data; boundary=----test",
        }),
        redirect: "manual",
      }),
    );
    const [, forwarded] = fetchSpy.mock.calls[0] as [
      string,
      { body: ArrayBuffer },
    ];
    expect(new Uint8Array(forwarded.body)).toEqual(uploadBytes);
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-upload");
  });
});
