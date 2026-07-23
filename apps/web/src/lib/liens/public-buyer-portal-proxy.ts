import { type NextRequest, NextResponse } from "next/server";

const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";

export async function proxyPublicBuyerPortal(
  req: NextRequest,
  token: string,
): Promise<NextResponse> {
  const normalizedToken = token.trim();
  if (!normalizedToken) {
    return new NextResponse("Missing buyer access token.", {
      status: 400,
      headers: { "content-type": "text/plain; charset=utf-8" },
    });
  }

  const targetUrl =
    `${GATEWAY_URL}/liens/api/liens/selling/public/` +
    `${encodeURIComponent(normalizedToken)}${req.nextUrl.search}`;
  const headers: Record<string, string> = {};
  const accept = req.headers.get("accept");
  if (accept) headers.accept = accept;
  headers["x-legal-synq-public-host"] =
    req.headers.get("host") ?? req.nextUrl.host;
  headers["x-legal-synq-public-proto"] = req.nextUrl.protocol.replace(/:$/, "");

  const res = await fetch(targetUrl, {
    method: "GET",
    headers,
    cache: "no-store",
  });

  const responseHeaders: Record<string, string> = {
    "content-type": res.headers.get("content-type") ?? "text/html; charset=utf-8",
  };
  const correlationId = res.headers.get("x-correlation-id");
  if (correlationId) responseHeaders["x-correlation-id"] = correlationId;

  return new NextResponse(await res.text(), {
    status: res.status,
    headers: responseHeaders,
  });
}
