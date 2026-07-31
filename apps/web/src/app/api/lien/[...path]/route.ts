import { type NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

/**
 * Catch-all BFF proxy for all SynqLien client-side API calls.
 *
 * Client Components call  /api/lien/api/liens/...
 * This handler forwards    → GATEWAY_URL/liens/api/liens/...
 * with the session cookie forwarded as Authorization: Bearer.
 *
 * The gateway YARP route matches `/liens/{**catch-all}` (plural) and strips
 * the `/liens` prefix before forwarding to the Liens service on :5009.
 *
 * Cookie reading: uses cookies() from next/headers (server-side store) rather
 * than request.cookies — more reliable inside App Router Route Handlers.
 */
const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";

async function proxy(
  req: NextRequest,
  segments: string[],
): Promise<NextResponse> {
  const path = segments.join("/");
  const search = req.nextUrl.search;
  const url = `${GATEWAY_URL}/liens/${path}${search}`;
  const cookieStore = await cookies();
  // Support both portal users (portal_session) and platform/admin users (platform_session).
  const token =
    cookieStore.get("portal_session")?.value ??
    cookieStore.get("platform_session")?.value;
  const incomingContentType = req.headers.get("Content-Type") ?? "";
  const isMultipart = incomingContentType.startsWith("multipart/form-data");

  const headers: Record<string, string> = {};
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const idempotencyKey = req.headers.get("Idempotency-Key");
  if (idempotencyKey) {
    headers["Idempotency-Key"] = idempotencyKey;
  }

  const publicHost = req.headers.get("host");
  if (publicHost) {
    headers["x-legal-synq-public-host"] = publicHost;
  }

  const publicProto = req.headers.get("x-forwarded-proto") ?? req.nextUrl.protocol.replace(/:$/, "");
  if (publicProto) {
    headers["x-legal-synq-public-proto"] = publicProto;
  }

  let body: ArrayBuffer | string | undefined;
  if (req.method !== "GET" && req.method !== "HEAD") {
    if (isMultipart) {
      headers["Content-Type"] = incomingContentType;
      try {
        body = await req.arrayBuffer();
      } catch {
        /* no body */
      }
    } else {
      headers["Content-Type"] = "application/json";
      try {
        body = await req.text();
      } catch {
        /* no body */
      }
    }
  }

  const res = await fetch(url, {
    method: req.method,
    headers,
    body,
    redirect: "manual",
  });

  const responseHeaders: Record<string, string> = {};
  const correlationId = res.headers.get("X-Correlation-Id");
  if (correlationId) responseHeaders["X-Correlation-Id"] = correlationId;
  const isRedirect = res.status >= 300 && res.status < 400;
  if (isRedirect) {
    const location = res.headers.get("Location");
    if (location) responseHeaders["Location"] = rewriteRedirectLocation(location);
  } else {
    responseHeaders["Content-Type"] =
      res.headers.get("Content-Type") ?? "application/json";
  }

  if (res.status === 204) {
    return new NextResponse(null, { status: 204, headers: responseHeaders });
  }

  if (isRedirect) {
    return new NextResponse(null, {
      status: res.status,
      headers: responseHeaders,
    });
  }

  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: responseHeaders,
  });
}

function rewriteRedirectLocation(location: string): string {
  if (location.startsWith("/documents/access/")) {
    return `/api/lien${location}`;
  }

  if (location.startsWith("/access/")) {
    return `/api/lien/documents${location}`;
  }

  try {
    const parsed = new URL(location);
    if (parsed.pathname.startsWith("/documents/access/")) {
      return `/api/lien${parsed.pathname}${parsed.search}${parsed.hash}`;
    }
  } catch {
    /* keep relative non-document redirects as-is */
  }

  return location;
}

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  return proxy(req, (await params).path);
}
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  return proxy(req, (await params).path);
}
export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  return proxy(req, (await params).path);
}
export async function PATCH(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  return proxy(req, (await params).path);
}
export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  return proxy(req, (await params).path);
}
