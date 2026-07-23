import { describe, expect, it } from "vitest";
import { NextRequest } from "next/server";
import { proxy } from "./proxy";

function request(path: string): NextRequest {
  return new NextRequest(`https://tenant.legalsynq.test${path}`);
}

describe("proxy", () => {
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

  it("allows SynqLien public buyer offer gateway rewrite paths without a platform session", () => {
    const response = proxy(
      request("/api/liens/api/liens/selling/public/test-token/accept"),
    );

    expect(response.headers.get("location")).toBeNull();
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
