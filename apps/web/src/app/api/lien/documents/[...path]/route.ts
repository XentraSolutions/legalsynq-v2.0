import { type NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

/**
 * Catch-all BFF proxy for SynqLien's Documents-service client-side API calls.
 *
 * Client Components call  /api/lien/documents/documents/{id}
 * This handler forwards    → GATEWAY_URL/documents/documents/{id}
 * with the session cookie forwarded as Authorization: Bearer.
 *
 * Namespaced under /api/lien/ (rather than a top-level /api/documents/
 * catch-all) so it can't collide with CareConnect's own document/attachment
 * routing at the top level — a top-level /api/documents/[...path] proxy
 * previously shadowed CareConnect's document access paths.
 *
 * The gateway validates JWT from the Authorization header only — the
 * portal_session/platform_session token lives in an HttpOnly cookie that
 * client-side JS can't read, so this handler bridges the gap.
 *
 * Cookie reading: uses cookies() from next/headers (server-side store) rather
 * than request.cookies — more reliable inside App Router Route Handlers.
 */
const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";
const LEGACY_LINK_PREFIX = "legacy-links";

function getSessionToken(cookieStore: Awaited<ReturnType<typeof cookies>>) {
  return cookieStore.get("portal_session")?.value ?? cookieStore.get("platform_session")?.value;
}

function isLegacyViewUrlRequest(segments: string[]): boolean {
  return segments.length === 3
    && segments[0] === "documents"
    && segments[2] === "view-url"
    && !isGuid(segments[1]);
}

function isLegacyRedeemRequest(segments: string[]): boolean {
  return segments.length === 2 && segments[0] === LEGACY_LINK_PREFIX;
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

async function resolveLegacyDocumentUrl(objectKey: string, token: string | undefined): Promise<Response> {
  const headers: Record<string, string> = {};
  if (token) headers.Authorization = `Bearer ${token}`;

  return fetch(
    `${GATEWAY_URL}/liens/api/liens/legacy-document-links/${encodeURIComponent(objectKey)}/resolve`,
    { headers, cache: "no-store" },
  );
}

async function issueLegacyViewUrl(objectKey: string): Promise<NextResponse> {
  const token = getSessionToken(await cookies());
  const resolved = await resolveLegacyDocumentUrl(objectKey, token);
  if (!resolved.ok) {
    return new NextResponse(resolved.body, {
      status: resolved.status,
      headers: { "Content-Type": resolved.headers.get("Content-Type") ?? "application/json" },
    });
  }

  return NextResponse.json({
    data: { redeemUrl: `/${LEGACY_LINK_PREFIX}/${encodeURIComponent(objectKey)}` },
  });
}

async function redeemLegacyViewUrl(objectKey: string): Promise<NextResponse> {
  const token = getSessionToken(await cookies());
  const resolved = await resolveLegacyDocumentUrl(objectKey, token);
  if (!resolved.ok) {
    return new NextResponse(resolved.body, {
      status: resolved.status,
      headers: { "Content-Type": resolved.headers.get("Content-Type") ?? "application/json" },
    });
  }

  const body = await resolved.json() as { url?: unknown };
  if (typeof body.url !== "string" || !isLegacyDocumentUrl(body.url)) {
    return NextResponse.json({ error: { code: "invalid_legacy_document_url" } }, { status: 502 });
  }

  return NextResponse.redirect(body.url, 302);
}

function isLegacyDocumentUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === "https:"
      && url.hostname === "legal-dmm-prod.legalsynq.com"
      && url.port === "";
  } catch {
    return false;
  }
}

async function proxy(
  req: NextRequest,
  segments: string[],
): Promise<NextResponse> {
  const path = segments.join("/");
  const search = req.nextUrl.search;
  const url = `${GATEWAY_URL}/documents/${path}${search}`;
  const cookieStore = await cookies();
  // Support both portal users (portal_session) and platform/admin users (platform_session).
  const token = getSessionToken(cookieStore);
  const incomingContentType = req.headers.get("Content-Type") ?? "";
  const isMultipart = incomingContentType.startsWith("multipart/form-data");

  const headers: Record<string, string> = {};
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
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
    const contentType = res.headers.get("Content-Type") ?? "application/json";
    responseHeaders["Content-Type"] = contentType;
    copyHeader(res, responseHeaders, "Content-Disposition");
    copyHeader(res, responseHeaders, "Accept-Ranges");
    copyHeader(res, responseHeaders, "Content-Range");
    copyHeader(res, responseHeaders, "Cache-Control");
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

  const contentType = responseHeaders["Content-Type"] ?? "";
  const isTextOrJson =
    contentType.startsWith("application/json") ||
    contentType.startsWith("text/") ||
    contentType.startsWith("application/problem");

  const data = isTextOrJson ? await res.text() : res.body;
  return new NextResponse(data, {
    status: res.status,
    headers: responseHeaders,
  });
}

function copyHeader(
  source: Response,
  target: Record<string, string>,
  headerName: string,
) {
  const value = source.headers.get(headerName);
  if (value) target[headerName] = value;
}

function rewriteRedirectLocation(location: string): string {
  if (location.startsWith("/access/") || location.startsWith("/internal/")) {
    return `/api/lien/documents${location}`;
  }

  if (location.startsWith("/documents/access/") || location.startsWith("/documents/internal/")) {
    return `/api/lien${location}`;
  }

  try {
    const parsed = new URL(location);
    if (parsed.pathname.startsWith("/access/") || parsed.pathname.startsWith("/internal/")) {
      return `/api/lien/documents${parsed.pathname}${parsed.search}${parsed.hash}`;
    }

    if (parsed.pathname.startsWith("/documents/access/") || parsed.pathname.startsWith("/documents/internal/")) {
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
  const path = (await params).path;
  return isLegacyRedeemRequest(path)
    ? redeemLegacyViewUrl(path[1])
    : proxy(req, path);
}
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const path = (await params).path;
  return isLegacyViewUrlRequest(path)
    ? issueLegacyViewUrl(path[1])
    : proxy(req, path);
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
