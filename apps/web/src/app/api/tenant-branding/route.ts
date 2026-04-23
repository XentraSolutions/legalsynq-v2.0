import { NextRequest, NextResponse } from 'next/server';

/**
 * Read-source-aware tenant branding BFF endpoint.
 *
 * Replaces the hard-wired `/api/identity/api/tenants/current/branding` call
 * in TenantBrandingProvider with a configurable, source-agnostic proxy.
 *
 * Mode selection (TENANT_BRANDING_READ_SOURCE env var, default: Identity):
 *   Identity      — legacy path: forwards to Identity service (no behavior change)
 *   Tenant        — reads from Tenant service public branding endpoint
 *   HybridFallback — tries Tenant first, falls back to Identity on failure/404/incomplete
 *
 * Response is always in TenantBranding shape regardless of source.
 * The client (TenantBrandingProvider) is fully source-agnostic.
 *
 * Observability: every call logs mode, source used, fallback trigger + reason.
 */

const GATEWAY_URL  = process.env.GATEWAY_URL  ?? 'http://127.0.0.1:5010';
const READ_SOURCE  = (process.env.TENANT_BRANDING_READ_SOURCE ?? 'Identity') as ReadSource;

type ReadSource = 'Identity' | 'Tenant' | 'HybridFallback';

interface TenantBrandingShape {
  tenantId?:           string;
  tenantCode?:         string;
  displayName?:        string;
  primaryColor?:       string;
  logoDocumentId?:     string;
  logoWhiteDocumentId?: string;
  faviconUrl?:         string;
}

// ── Tenant code resolution ────────────────────────────────────────────────────

function resolveTenantCode(req: NextRequest): string | null {
  const headerCode = req.headers.get('x-tenant-code');
  if (headerCode) return headerCode;

  const host = req.headers.get('x-forwarded-host') ?? req.headers.get('host') ?? '';
  const parts = host.split('.');
  if (parts.length >= 3 && !host.startsWith('localhost')) return parts[0];

  const devCode = process.env.NEXT_PUBLIC_TENANT_CODE;
  if (devCode) return devCode;

  return null;
}

// ── Identity fetch ────────────────────────────────────────────────────────────

async function fetchFromIdentity(
  tenantCode: string,
  req: NextRequest,
): Promise<TenantBrandingShape | null> {
  const host = req.headers.get('x-forwarded-host') ?? req.headers.get('host');

  const headers: Record<string, string> = {
    'X-Tenant-Code': tenantCode,
  };
  if (host) headers['X-Forwarded-Host'] = host;

  const res = await fetch(
    `${GATEWAY_URL}/identity/api/tenants/current/branding`,
    { headers },
  );

  if (!res.ok) return null;

  const data = await res.json();
  return data as TenantBrandingShape;
}

// ── Tenant service fetch ──────────────────────────────────────────────────────

async function fetchFromTenant(tenantCode: string): Promise<TenantBrandingShape | null> {
  const url = `${GATEWAY_URL}/tenant/api/v1/public/branding/by-code/${encodeURIComponent(tenantCode)}`;
  const res = await fetch(url);

  if (!res.ok) return null;

  const data = await res.json();

  return {
    tenantId:            data.tenantId           ?? undefined,
    tenantCode:          data.code               ?? undefined,
    displayName:         data.displayName        ?? undefined,
    primaryColor:        data.primaryColor       ?? undefined,
    logoDocumentId:      data.logoDocumentId     ?? undefined,
    logoWhiteDocumentId: data.logoWhiteDocumentId ?? undefined,
  };
}

// ── Usability check ───────────────────────────────────────────────────────────

function isUsable(b: TenantBrandingShape | null): b is TenantBrandingShape {
  return !!(b && b.tenantId && (b.tenantCode) && b.displayName);
}

// ── Route handler ─────────────────────────────────────────────────────────────

export async function GET(req: NextRequest): Promise<NextResponse> {
  const tenantCode = resolveTenantCode(req);

  if (!tenantCode) {
    console.warn('[tenant-branding] Tenant code could not be resolved from request');
    return NextResponse.json({ message: 'Tenant code could not be resolved' }, { status: 404 });
  }

  let branding: TenantBrandingShape | null = null;
  let source: string = 'none';
  let fallbackTriggered = false;
  let fallbackReason: string | undefined;

  if (READ_SOURCE === 'Tenant') {
    try {
      branding = await fetchFromTenant(tenantCode);
      if (branding) source = 'tenant';
    } catch (err) {
      console.error('[tenant-branding] Tenant read failed', {
        tenantCode,
        error: String(err),
      });
    }

  } else if (READ_SOURCE === 'HybridFallback') {
    try {
      const tenantResult = await fetchFromTenant(tenantCode);
      if (isUsable(tenantResult)) {
        branding = tenantResult;
        source   = 'tenant';
      } else {
        fallbackTriggered = true;
        fallbackReason    = tenantResult ? 'incomplete_fields' : 'not_found';
      }
    } catch (err) {
      fallbackTriggered = true;
      fallbackReason    = 'tenant_unavailable';
      console.warn('[tenant-branding] Tenant fetch failed, falling back to Identity', {
        tenantCode,
        error: String(err),
      });
    }

    if (!branding) {
      try {
        branding = await fetchFromIdentity(tenantCode, req);
        if (branding) source = 'identity';
      } catch (err) {
        console.error('[tenant-branding] Identity fallback also failed', {
          tenantCode,
          error: String(err),
        });
      }
    }

  } else {
    // Identity mode — default, legacy-equivalent behavior
    try {
      branding = await fetchFromIdentity(tenantCode, req);
      if (branding) source = 'identity';
    } catch (err) {
      console.error('[tenant-branding] Identity read failed', {
        tenantCode,
        error: String(err),
      });
    }
  }

  console.log('[tenant-branding]', JSON.stringify({
    mode:             READ_SOURCE,
    source,
    tenantCode,
    fallbackTriggered,
    fallbackReason,
    resolved:         !!branding,
  }));

  if (!branding) {
    return NextResponse.json({ message: 'Tenant branding not found' }, { status: 404 });
  }

  return NextResponse.json(branding);
}
