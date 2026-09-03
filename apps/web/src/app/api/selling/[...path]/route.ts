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

// ASSUMPTION, not confirmed by the Liens service: if a lien's
// availableActions allows "prepare-sale", we assume "keep" is also allowed,
// since the backend never actually returns "keep" today. Applied here (the
// BFF boundary) rather than in client code so every caller sees it
// consistently. Drop this once the backend returns "keep" itself.
function withKeepAssumption(actions: unknown): unknown {
  if (!Array.isArray(actions)) return actions;
  if (actions.includes("prepare-sale") && !actions.includes("keep")) {
    return [...actions, "keep"];
  }
  return actions;
}

// Matches GET .../api/liens/selling/liens (list) and
// .../api/liens/selling/liens/{id} (detail) — not deeper subpaths like
// .../liens/{id}/activity, which have their own response shapes.
function isLienListOrDetailPath(segments: string[]): boolean {
  return (
    segments.length >= 4 &&
    segments.length <= 5 &&
    segments[0] === "api" &&
    segments[1] === "liens" &&
    segments[2] === "selling" &&
    segments[3] === "liens"
  );
}

function applyKeepAssumption(rawJson: string): string {
  const parsed = JSON.parse(rawJson);
  if (Array.isArray(parsed?.items)) {
    for (const item of parsed.items) {
      if (item && typeof item === "object") {
        item.availableActions = withKeepAssumption(item.availableActions);
      }
    }
  } else if (parsed && typeof parsed === "object") {
    parsed.availableActions = withKeepAssumption(parsed.availableActions);
  }
  return JSON.stringify(parsed);
}

async function proxy(
  req: NextRequest,
  segments: string[],
): Promise<NextResponse> {
  const path = segments.join("/");
  const search = req.nextUrl.search;
  const url = `${GATEWAY_URL}/liens/${path}${search}`;
  // console.log("PROXY", url);
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

  let data = await res.text();
  if (
    req.method === "GET" &&
    res.ok &&
    isLienListOrDetailPath(segments) &&
    (res.headers.get("Content-Type") ?? "").includes("application/json")
  ) {
    try {
      data = applyKeepAssumption(data);
    } catch {
      /* not the JSON shape we expected — pass the response through as-is */
    }
  }

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
