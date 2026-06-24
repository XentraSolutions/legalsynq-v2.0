import { NextResponse }             from 'next/server';
import { requirePlatformAdmin }     from '@/lib/auth-guards';
import { BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';
import type { BillingEntitlementSnapshot } from '@/types/control-center';

export const dynamic = 'force-dynamic';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function empty(tenantId: string, error: string): BillingEntitlementSnapshot {
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

export async function GET(
  _req: Request,
  { params }: { params: Promise<{ tenantId: string }> },
) {
  await requirePlatformAdmin();
  const { tenantId } = await params;

  if (!UUID_RE.test(tenantId)) {
    return NextResponse.json(
      empty(tenantId, 'Invalid tenantId — must be a UUID.'),
      { status: 400, headers: { 'Cache-Control': 'no-store' } },
    );
  }

  const token = BILLING_INTERNAL_TOKEN;
  if (!token) {
    return NextResponse.json(
      empty(tenantId, 'Billing service internal token is not configured. Set BILLING_INTERNAL_TOKEN.'),
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
    const [snapshotRes, accessRes] = await Promise.allSettled([
      fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/entitlements/current`, {
        headers,
        signal: AbortSignal.timeout(5000),
      }),
      fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/entitlements/access`, {
        headers,
        signal: AbortSignal.timeout(5000),
      }),
    ]);

    let snapshot: Record<string, unknown> | null = null;
    let access:   Record<string, unknown> | null = null;
    let snapshotError: string | null = null;

    if (snapshotRes.status === 'fulfilled') {
      const r = snapshotRes.value;
      if (r.ok) {
        try { snapshot = await r.json(); } catch { /* ignore */ }
      } else if (r.status === 404) {
        snapshotError = null;
      } else if (r.status === 401 || r.status === 403) {
        snapshotError = `Billing service rejected request (HTTP ${r.status}).`;
      } else {
        snapshotError = `Billing service returned HTTP ${r.status}.`;
      }
    } else {
      snapshotError = 'Billing service unreachable.';
    }

    if (accessRes.status === 'fulfilled') {
      const r = accessRes.value;
      if (r.ok) {
        try { access = await r.json(); } catch { /* ignore */ }
      }
    }

    if (snapshotError && !access) {
      return NextResponse.json(
        empty(tenantId, snapshotError),
        { headers: { 'Cache-Control': 'no-store' } },
      );
    }

    const payload: BillingEntitlementSnapshot = {
      profileId:            snapshot ? String(snapshot['tenantBillingProfileId'] ?? '') || null : null,
      billingAccountId:     snapshot ? String(snapshot['billingAccountId'] ?? '') || null : null,
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
      error:                snapshotError,
    };

    return NextResponse.json(payload, { headers: { 'Cache-Control': 'no-store' } });
  } catch {
    return NextResponse.json(
      empty(tenantId, 'Billing entitlement service unreachable.'),
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }
}
