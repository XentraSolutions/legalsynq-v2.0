import { NextRequest } from "next/server";
import { cookies } from "next/headers";
import { beforeEach, describe, expect, test, vi } from "vitest";

vi.mock("next/headers", () => ({
  cookies: vi.fn(),
}));

type RouteContext = { params: Promise<{ path: string[] }> };

function makeRequest(path: string, method = "GET"): [NextRequest, RouteContext] {
  const request = new NextRequest(`http://localhost/api/lien/documents/${path}`, {
    method,
  });
  return [
    request,
    { params: Promise.resolve({ path: path.split("?")[0].split("/") }) },
  ];
}

describe("SynqLien documents proxy", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue(undefined),
    } as never);
  });

  test("rewrites local file redirects through the same-origin documents proxy", async () => {
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(null, {
        status: 302,
        headers: {
          Location: "/internal/files?token=file-token&disposition=view",
          "X-Correlation-Id": "corr-local-file",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("access/abc123");

    const response = await GET(req, ctx);

    expect(fetchSpy).toHaveBeenCalledWith(
      "http://127.0.0.1:5010/documents/access/abc123",
      expect.objectContaining({
        method: "GET",
        redirect: "manual",
      }),
    );
    expect(response.status).toBe(302);
    expect(response.headers.get("Location")).toBe(
      "/api/lien/documents/internal/files?token=file-token&disposition=view",
    );
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-local-file");
  });

  test("streams binary document responses without text re-encoding", async () => {
    const fileBytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(fileBytes, {
        status: 200,
        headers: {
          "Content-Type": "image/png",
          "Content-Disposition": 'attachment; filename="Lorem-Ipsum.png"',
          "Accept-Ranges": "bytes",
          "X-Correlation-Id": "corr-file-bytes",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { GET } = await import("./route");
    const [req, ctx] = makeRequest("internal/files?token=file-token&disposition=download");

    const response = await GET(req, ctx);

    expect(response.status).toBe(200);
    expect(response.headers.get("Content-Type")).toBe("image/png");
    expect(response.headers.get("Content-Disposition")).toBe(
      'attachment; filename="Lorem-Ipsum.png"',
    );
    expect(response.headers.get("Accept-Ranges")).toBe("bytes");
    expect(response.headers.get("X-Correlation-Id")).toBe("corr-file-bytes");
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(fileBytes);
  });
});
