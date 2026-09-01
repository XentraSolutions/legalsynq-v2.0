import { NextRequest } from "next/server";
import { cookies } from "next/headers";
import { beforeEach, describe, expect, test, vi } from "vitest";

vi.mock("next/headers", () => ({
  cookies: vi.fn(),
}));

vi.mock("@/lib/pdf-merge.service", () => ({
  mergePdfsFromUrls: vi.fn(),
}));

function makeRequest(body: { urls: string[] }): NextRequest {
  return new NextRequest("http://localhost/api/lien/pdf-merge", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

describe("POST /api/lien/pdf-merge", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue(undefined),
    } as never);
  });

  test("returns 401 when no session token is present", async () => {
    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["/api/lien/documents/access/doc1"],
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(401);
    expect(body.error.code).toBe("unauthorized");
  });

  test("returns 400 when urls array is empty", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const { POST } = await import("./route");
    const request = makeRequest({ urls: [] });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(400);
    expect(body.error.code).toBe("invalid_urls");
  });

  test("returns 400 when urls is not an array", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const { POST } = await import("./route");
    const request = new NextRequest("http://localhost/api/lien/pdf-merge", {
      method: "POST",
      body: JSON.stringify({ urls: "not-an-array" }),
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(400);
    expect(body.error.code).toBe("invalid_urls");
  });

  test("returns 400 when URL does not start with /api/lien/", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["https://evil.com/document"],
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(400);
    expect(body.error.code).toBe("invalid_url");
  });

  test("returns 400 when URL is not a string", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const { POST } = await import("./route");
    const request = new NextRequest("http://localhost/api/lien/pdf-merge", {
      method: "POST",
      body: JSON.stringify({ urls: [123] }),
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(400);
    expect(body.error.code).toBe("invalid_url");
  });

  test("successfully merges PDFs and returns base64-encoded response", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const mockPdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46]); // %PDF
    const { mergePdfsFromUrls } = await import("@/lib/pdf-merge.service");
    vi.mocked(mergePdfsFromUrls).mockResolvedValue(mockPdfBytes);

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: [
        "/api/lien/documents/access/28c4792c056e21600e56255f8c66118601868d2a5d170f2f76de35eeaa8237a8",
        "/api/lien/documents/access/838e96b9bc2a0cb1cd81b54b24fb213185bdf53f6bdaace91a4c2b725d3b05cc",
      ],
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(200);
    expect(body.data).toBeDefined();
    expect(typeof body.data).toBe("string");
    // Verify it's valid base64
    expect(() => Buffer.from(body.data, "base64")).not.toThrow();
  });

  test("converts relative URLs to absolute URLs before merging", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const mockPdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46]);
    const { mergePdfsFromUrls } = await import("@/lib/pdf-merge.service");
    const mergeSpy = vi
      .mocked(mergePdfsFromUrls)
      .mockResolvedValue(mockPdfBytes);

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["/api/lien/documents/access/doc1"],
    });

    const response = await POST(request);

    expect(response.status).toBe(200);
    expect(mergeSpy).toHaveBeenCalledWith(
      expect.arrayContaining([
        expect.stringContaining(
          "http://localhost/api/lien/documents/access/doc1",
        ),
      ]),
      "test-token",
    );
  });

  test("passes auth token to mergePdfsFromUrls", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "my-auth-token" }),
    } as never);

    const mockPdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46]);
    const { mergePdfsFromUrls } = await import("@/lib/pdf-merge.service");
    const mergeSpy = vi
      .mocked(mergePdfsFromUrls)
      .mockResolvedValue(mockPdfBytes);

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["/api/lien/documents/access/doc1"],
    });

    await POST(request);

    expect(mergeSpy).toHaveBeenCalledWith(expect.any(Array), "my-auth-token");
  });

  test("uses portal_session cookie if platform_session is not present", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn((name: string) => {
        if (name === "platform_session") return undefined;
        if (name === "portal_session") return { value: "portal-token" };
        return undefined;
      }),
    } as never);

    const mockPdfBytes = new Uint8Array([0x25, 0x50, 0x44, 0x46]);
    const { mergePdfsFromUrls } = await import("@/lib/pdf-merge.service");
    const mergeSpy = vi
      .mocked(mergePdfsFromUrls)
      .mockResolvedValue(mockPdfBytes);

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["/api/lien/documents/access/doc1"],
    });

    const response = await POST(request);

    expect(response.status).toBe(200);
    expect(mergeSpy).toHaveBeenCalledWith(expect.any(Array), "portal-token");
  });

  test("returns 500 when PDF merge fails", async () => {
    vi.mocked(cookies).mockResolvedValue({
      get: vi.fn().mockReturnValue({ value: "test-token" }),
    } as never);

    const { mergePdfsFromUrls } = await import("@/lib/pdf-merge.service");
    vi.mocked(mergePdfsFromUrls).mockRejectedValue(
      new Error("Failed to load PDF"),
    );

    const { POST } = await import("./route");
    const request = makeRequest({
      urls: ["/api/lien/documents/access/doc1"],
    });

    const response = await POST(request);
    const body = await response.json();

    expect(response.status).toBe(500);
    expect(body.error.code).toBe("merge_failed");
    expect(body.error.message).toContain("Failed to load PDF");
  });
});
