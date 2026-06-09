import { NextResponse }         from 'next/server';
import { requireAdmin }         from '@/lib/auth-guards';
import { BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';
import type { TenantAdminBillingStatus, TenantBillingProfile, BillingEntitlementSnapshot } from '@/types/control-center';

export const dynamic = 'force-dynamic';

function emptyStatus(tenantId: string, error: string): TenantAdminBillingStatus {
  return {
    tenantId,
    profileFound:     false,
    profile:          null,
    entitlement:      null,
    lastCheckedAtUtc: new Date().toISOString(),
    error,
  };
}

function emptyEntitlement(error: string | null): BillingEntitlementSnapshot {
  return {
    profileId:            null,
    billingAccountId:     null,
    entitlementStatus:    'Unknown',
    accessRecommendation: 'Unknown',
    isEnabled:            false,
    writeAccessAllowed:   false,
    sourcePlanKey:        null,
    sourceProductKey:     null,
    effectiveFromUtc:     null,
    effectiveToUtc:       null,
    lastSyncedAtUtc:      null,
    lastCheckedAtUtc:     new Date().toISOString(),
    error,
  };
}

/**
 * GET /api/billing/my-billing-status
 *
 * Returns the billing profile + entitlement status for the calling user's
 * own tenant.
 *
 * - TenantAdmin: always uses session.tenantId — cannot specify a different tenant
 * - PlatformAdmin: uses session.tenantId by default; can pass ?tenantId=<uuid>
 *   to view any tenant (cross-tenant operational visibility)
 *
 * Access: requireAdmin() — PlatformAdmin OR TenantAdmin
 */
export async function GET(req: Request) {
  const session = await requireAdmin();

  const url      = new URL(req.url);
  const qTenant  = url.searchParams.get('tenantId');

  let tenantId: string;

  if (session.isPlatformAdmin && qTenant) {
    tenantId = qTenant;
  } else {
    if (!session.tenantId) {
      return NextResponse.json(
        emptyStatus('', 'No tenant associated with this session.'),
        { status: 400, headers: { 'Cache-Control': 'no-store' } },
      );
    }
    tenantId = session.tenantId;
    if (!session.isPlatformAdmin && qTenant && qTenant !== tenantId) {
      return NextResponse.json(
        { message: 'Forbidden — you may only view your own tenant billing status.' },
        { status: 403 },
      );
    }
  }

  const token = BILLING_INTERNAL_TOKEN;
  if (!token) {
    return NextResponse.json(
      emptyStatus(tenantId, 'Billing service internal token is not configured.'),
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }

  const headers: HeadersInit = {
    'X-Internal-Token': token,
    'X-Tenant-Id':      tenantId,
    'Accept':           'application/json',
  };

  const now = new Date().toISOString();

  try {
    const [profilesRes, entitlementRes, accessRes] = await Promise.allSettled([
      fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/profiles`, {
        headers,
        signal: AbortSignal.timeout(5000),
      }),
      fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/entitlements/current`, {
        headers,
        signal: AbortSignal.timeout(5000),
      }),
      fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/entitlements/access`, {
        headers,
        signal: AbortSignal.timeout(5000),
      }),
    ]);

    let profile:    TenantBillingProfile | null = null;
    let profileErr: string | null               = null;
    let snapshot:   Record<string, unknown> | null = null;
    let access:     Record<string, unknown> | null = null;

    if (profilesRes.status === 'fulfilled') {
      const r = profilesRes.value;
      if (r.ok) {
        try {
          const body = await r.json() as { items?: unknown[] };
          const items = body.items ?? [];
          if (items.length > 0) {
            const p = items[0] as Record<string, unknown>;
            profile = {
              id:               String(p['id'] ?? ''),
              tenantId:         String(p['tenantId'] ?? ''),
              billingAccountId: String(p['billingAccountId'] ?? ''),
              hostPlatformKey:  p['hostPlatformKey'] ? String(p['hostPlatformKey']) : null,
              externalTenantId: p['externalTenantId'] ? String(p['externalTenantId']) : null,
              status:           String(p['status'] ?? ''),
              mode:             String(p['mode'] ?? ''),
              createdAtUtc:     String(p['createdAtUtc'] ?? ''),
              updatedAtUtc:     String(p['updatedAtUtc'] ?? ''),
              activatedAtUtc:   p['activatedAtUtc'] ? String(p['activatedAtUtc']) : null,
              closedAtUtc:      p['closedAtUtc'] ? String(p['closedAtUtc']) : null,
            };
          }
        } catch { /* ignore parse errors */ }
      } else if (r.status === 401 || r.status === 403) {
        profileErr = `Billing service rejected request (HTTP ${r.status}).`;
      } else {
        profileErr = `Billing service returned HTTP ${r.status}.`;
      }
    } else {
      profileErr = 'Billing service unreachable.';
    }

    if (entitlementRes.status === 'fulfilled' && entitlementRes.value.ok) {
      try { snapshot = await entitlementRes.value.json(); } catch { /* ignore */ }
    }
    if (accessRes.status === 'fulfilled' && accessRes.value.ok) {
      try { access = await accessRes.value.json(); } catch { /* ignore */ }
    }

    const entitlement: BillingEntitlementSnapshot = snapshot || access
      ? {
          profileId:            snapshot ? (String(snapshot['tenantBillingProfileId'] ?? '') || null) : null,
          billingAccountId:     snapshot ? (String(snapshot['billingAccountId'] ?? '') || null) : null,
          entitlementStatus:    String(
            (access ? access['entitlementStatus'] : null) ??
            (snapshot ? snapshot['entitlementStatus'] : null) ??
            'Unknown',
          ),
          accessRecommendation: String(
            (access ? access['accessRecommendation'] : null) ??
            (snapshot ? snapshot['accessRecommendation'] : null) ??
            'Unknown',
          ),
          isEnabled:            Boolean(access ? access['isEnabled'] : false),
          writeAccessAllowed:   Boolean(access ? access['writeAccessAllowed'] : false),
          sourcePlanKey:        snapshot ? (String(snapshot['sourcePlanKey'] ?? '') || null) : null,
          sourceProductKey:     snapshot ? (String(snapshot['sourceProductKey'] ?? '') || null) : null,
          effectiveFromUtc:     snapshot ? (snapshot['effectiveFromUtc'] ? String(snapshot['effectiveFromUtc']) : null) : null,
          effectiveToUtc:       snapshot ? (snapshot['effectiveToUtc'] ? String(snapshot['effectiveToUtc']) : null) : null,
          lastSyncedAtUtc:      snapshot ? (snapshot['lastSyncedAtUtc'] ? String(snapshot['lastSyncedAtUtc']) : null) : null,
          lastCheckedAtUtc:     now,
          error:                null,
        }
      : emptyEntitlement(null);

    const result: TenantAdminBillingStatus = {
      tenantId,
      profileFound:     profile !== null,
      profile,
      entitlement,
      lastCheckedAtUtc: now,
      error:            profileErr,
    };

    return NextResponse.json(result, { headers: { 'Cache-Control': 'no-store' } });
  } catch {
    return NextResponse.json(
      emptyStatus(tenantId, 'Billing service unreachable.'),
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }
}
