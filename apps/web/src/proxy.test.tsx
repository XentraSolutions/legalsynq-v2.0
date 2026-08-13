import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { proxy } from "./proxy";

function request(path: string): NextRequest {
  return new NextRequest(`https://tenant.legalsynq.test${path}`);
}

function portalRequest(host: string, path: string): NextRequest {
  return new NextRequest(`http://${host}:3000${path}`, {
    headers: { host: `${host}:3000` },
  });
}

describe("proxy", () => {
  it("redirects the local tenant common-portal root to registration", () => {
    const response = proxy(portalRequest("tenant-demo.localhost", "/"));

    expect(response.headers.get("location")).toBe(
      "http://tenant-demo.localhost:3000/register",
    );
  });

  it("allows the tenant registration page without a platform session", () => {
    const response = proxy(portalRequest("tenant-demo.localhost", "/register"));

    expect(response.headers.get("location")).toBeNull();
    expect(response.status).toBe(200);
  });

  it("blocks the tenant registration page on localhost", () => {
    const response = proxy(portalRequest("localhost", "/register"));

    expect(response.status).toBe(404);
  });

  it("blocks tenant registration submissions on non-portal hosts", () => {
    const response = proxy(
      portalRequest(
        "localhost",
        "/api/tenant/api/v1/public/tenant-registrations",
      ),
    );

    expect(response.status).toBe(404);
  });

  it("allows clean SynqLien public buyer offer pages without a platform session", () => {
    const response = proxy(request("/selling/public/test-token"));

    expect(response.headers.get("location")).toBeNull();
  });

  it("allows SynqLien public buyer offer JSON paths without a platform session", () => {
    const response = proxy(
      request("/api/lien/api/liens/selling/public/test-token"),
    );

    expect(response.headers.get("location")).toBeNull();
  });

  it("redirects SynqLien public buyer offer gateway rewrite paths without a platform session", () => {
    const response = proxy(
      request("/api/liens/api/liens/selling/public/test-token/accept"),
    );

    expect(response.headers.get("location")).toContain(
      "/login?reason=unauthenticated",
    );
  });

  it("continues redirecting protected SynqLien API paths without a platform session", () => {
    const response = proxy(
      request("/api/lien/api/liens/selling/buyer/dashboard"),
    );

    expect(response.headers.get("location")).toContain(
      "/login?reason=unauthenticated",
    );
  });
});
