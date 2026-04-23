import { NextRequest, NextResponse } from 'next/server';

/**
 * TENANT-B07 — Source-aware public logo proxy.
 *
 * Reads TENANT_BRANDING_READ_SOURCE (default: Identity) to decide where to
 * fetch the tenant's logoDocumentId from:
 *
 *   Identity      — Identity service  /identity/api/tenants/current/branding
 *   Tenant        — Tenant service    /tenant/api/v1/public/branding/by-code/{code}
 *   HybridFallback — Tenant first, Identity fallback on failure/no-logo
 *
 * The actual image bytes are always proxied from the Documents service
 * (/documents/public/logo/{docId}) regardless of the branding read source.
 */

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://127.0.0.1:5010';
const READ_SOURCE = (process.env.TENANT_BRANDING_READ_SOURCE ?? 'Identity') as ReadSource;

const BRANDING_TIMEOUT_MS = 4_000;

type ReadSource = 'Identity' | 'Tenant' | 'HybridFallback';

// ── Tenant code resolution ────────────────────────────────────────────────────

function resolveTenantCode(req: NextRequest): string | null {
  const param = req.nextUrl.searchParams.get('tenantCode');
  if (param) return param;

  const host  = req.headers.get('x-forwarded-host') ?? req.headers.get('host') ?? '';
  const parts = host.split('.');
  if (parts.length >= 3) return parts[0];

  const env = process.env.NEXT_PUBLIC_ENV;
  if (env === 'development') return process.env.NEXT_PUBLIC_TENANT_CODE ?? null;

  return null;
}

// ── Branding fetch helpers ────────────────────────────────────────────────────

async function fetchLogoDocFromIdentity(tenantCode: string): Promise<string | null> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), BRANDING_TIMEOUT_MS);
  try {
    const res = await fetch(
      `${GATEWAY_URL}/identity/api/tenants/current/branding`,
      { headers: { 'X-Tenant-Code': tenantCode }, signal: controller.signal },
    );
    if (!res.ok) return null;
    const data = await res.json();
    return data?.logoDocumentId ?? null;
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

async function fetchLogoDocFromTenant(tenantCode: string): Promise<string | null> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), BRANDING_TIMEOUT_MS);
  try {
    const res = await fetch(
      `${GATEWAY_URL}/tenant/api/v1/public/branding/by-code/${encodeURIComponent(tenantCode)}`,
      { signal: controller.signal },
    );
    if (!res.ok) return null;
    const data = await res.json();
    return data?.logoDocumentId ?? null;
  } catch {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

// ── Route handler ─────────────────────────────────────────────────────────────

export async function GET(req: NextRequest) {
  const tenantCode = resolveTenantCode(req);

  if (!tenantCode) {
    return new NextResponse(null, { status: 404 });
  }

  let docId: string | null = null;
  let source = 'none';

  if (READ_SOURCE === 'Tenant') {
    docId  = await fetchLogoDocFromTenant(tenantCode);
    source = docId ? 'tenant' : 'none';

  } else if (READ_SOURCE === 'HybridFallback') {
    docId = await fetchLogoDocFromTenant(tenantCode);
    if (docId) {
      source = 'tenant';
    } else {
      docId  = await fetchLogoDocFromIdentity(tenantCode);
      source = docId ? 'identity_fallback' : 'none';
    }

  } else {
    // Identity mode — legacy behavior
    docId  = await fetchLogoDocFromIdentity(tenantCode);
    source = docId ? 'identity' : 'none';
  }

  console.log('[logo-public]', JSON.stringify({ mode: READ_SOURCE, source, tenantCode, hasDoc: !!docId }));

  if (!docId) {
    return new NextResponse(null, { status: 404 });
  }

  try {
    const logoRes = await fetch(`${GATEWAY_URL}/documents/public/logo/${docId}`);

    if (!logoRes.ok) {
      return new NextResponse(null, { status: logoRes.status });
    }

    const contentType = logoRes.headers.get('content-type') ?? 'image/png';
    const buffer      = await logoRes.arrayBuffer();

    return new NextResponse(buffer, {
      status: 200,
      headers: {
        'Content-Type':  contentType,
        'Cache-Control': 'public, max-age=3600, s-maxage=3600',
      },
    });
  } catch {
    return new NextResponse(null, { status: 502 });
  }
}
