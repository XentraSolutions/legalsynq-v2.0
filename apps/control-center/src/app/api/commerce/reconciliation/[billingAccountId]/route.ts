import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL, BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';
import type { ReconciliationDiagnostics, ReconciliationStatus } from '@/types/control-center';

const UUID_RE              = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const STALE_THRESHOLD_SECS = 86400; // 24 hours

function errResult(billingAccountId: string, error: string): ReconciliationDiagnostics {
  return {
    billingAccountId,
    tenantId:                     null,
    commerceAccessRecommendation: null,
    commerceAccountStanding:      null,
    commerceSnapshotGeneratedAt:  null,
    commerceSubscriptionCount:    null,
    commerceActiveSubscriptions:  null,
    billingEntitlementStatus:     null,
    billingAccessRecommendation:  null,
    billingLastSyncedAt:          null,
    billingEffectiveFrom:         null,
    reconciliationStatus:         'error',
    mismatchDetails:              null,
    staleDeltaSeconds:            null,
    staleThresholdSeconds:        STALE_THRESHOLD_SECS,
    lastCheckedAtUtc:             new Date().toISOString(),
    error,
    commerceError:                null,
    billingError:                 null,
  };
}

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ billingAccountId: string }> },
) {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { billingAccountId } = await params;

  if (!UUID_RE.test(billingAccountId)) {
    return NextResponse.json(
      errResult(billingAccountId, 'Invalid billingAccountId — must be a UUID.'),
      { status: 400 },
    );
  }

  const token = BILLING_INTERNAL_TOKEN;

  let tenantId:     string | null = null;
  let commerceRaw:  Record<string, unknown> | null = null;
  let billingRaw:   Record<string, unknown> | null = null;
  let commerceErr:  string | null = null;
  let billingErr:   string | null = null;

  // Step 1 — Resolve tenantId from Billing profile-by-billing-account
  if (token) {
    try {
      const profileRes = await fetch(
        `${BILLING_SERVICE_URL}/api/tenant-billing/profiles/by-billing-account/${billingAccountId}`,
        {
          cache: 'no-store',
          signal: AbortSignal.timeout(5000),
          headers: { 'X-Internal-Token': token, Accept: 'application/json' },
        },
      );
      if (profileRes.ok) {
        const p = await profileRes.json() as Record<string, unknown>;
        tenantId = typeof p.tenantId === 'string' ? p.tenantId : null;
      }
    } catch { /* tenantId remains null — billing may be unavailable */ }
  }

  // Step 2 — Fetch Commerce snapshot + Billing entitlement in parallel
  const [commerceResult, billingResult] = await Promise.allSettled([
    fetch(
      `${COMMERCE_SERVICE_URL}/api/commerce/integration/billing-accounts/${billingAccountId}/entitlement-snapshot`,
      { cache: 'no-store', signal: AbortSignal.timeout(6000) },
    ),
    token && tenantId
      ? fetch(
          `${BILLING_SERVICE_URL}/api/tenant-billing/entitlements/current`,
          {
            cache: 'no-store',
            signal: AbortSignal.timeout(6000),
            headers: {
              'X-Internal-Token': token,
              'X-Tenant-Id': tenantId,
              Accept: 'application/json',
            },
          },
        )
      : Promise.resolve(null),
  ]);

  if (commerceResult.status === 'fulfilled' && commerceResult.value !== null) {
    const r = commerceResult.value;
    if (r.ok) {
      try { commerceRaw = await r.json() as Record<string, unknown>; } catch { /* ignore */ }
    } else if (r.status === 404) {
      commerceErr = 'No Commerce snapshot found for this billing account.';
    } else if (r.status === 401 || r.status === 403) {
      commerceErr = 'Commerce identity integration not enabled (standalone mode).';
    } else {
      commerceErr = `Commerce returned HTTP ${r.status}.`;
    }
  } else if (commerceResult.status === 'rejected') {
    commerceErr = 'Commerce service unreachable.';
  }

  if (billingResult.status === 'fulfilled' && billingResult.value !== null) {
    const r = billingResult.value;
    if (r.ok) {
      try { billingRaw = await r.json() as Record<string, unknown>; } catch { /* ignore */ }
    } else if (r.status === 404) {
      billingErr = null; // No snapshot yet — not an error
    } else {
      billingErr = `Billing returned HTTP ${r.status}.`;
    }
  } else if (billingResult.status === 'rejected') {
    billingErr = 'Billing service unreachable.';
  } else if (!token) {
    billingErr = 'Billing internal token not configured.';
  } else if (!tenantId) {
    billingErr = 'Could not resolve tenant ID from billing account.';
  }

  // Step 3 — Derive reconciliation status
  const commerceRec = commerceRaw ? String(commerceRaw.accessRecommendation ?? '') : null;
  const billingRec  = billingRaw  ? String(billingRaw.accessRecommendation ?? '')  : null;
  const commerceGen = commerceRaw ? String(commerceRaw.generatedAtUtc ?? '')        : null;
  const billingSync = billingRaw  ? String(billingRaw.lastSyncedAtUtc ?? '')         : null;

  const subs = Array.isArray(commerceRaw?.subscriptions)
    ? (commerceRaw!.subscriptions as Array<Record<string, unknown>>)
    : null;
  const subCount       = subs ? subs.length : null;
  const activeSubCount = subs ? subs.filter(s => String(s.status ?? '').toLowerCase() === 'active').length : null;

  let status: ReconciliationStatus = 'unknown';
  let mismatch: string | null = null;
  let staleDelta: number | null = null;

  if (commerceErr && billingErr) {
    status = 'error';
  } else if (!commerceRaw && !billingRaw) {
    status = 'unknown';
  } else if (commerceRaw && billingRaw) {
    if (billingSync) {
      const syncDate   = new Date(billingSync);
      const nowMs      = Date.now();
      staleDelta = Math.floor((nowMs - syncDate.getTime()) / 1000);
      if (staleDelta > STALE_THRESHOLD_SECS) {
        status = 'stale';
      }
    }

    if (status !== 'stale' && commerceRec && billingRec && commerceRec !== billingRec) {
      status    = 'mismatch';
      mismatch  = `Commerce recommendation: ${commerceRec} — Billing recommendation: ${billingRec}`;
    } else if (status !== 'stale') {
      status = 'aligned';
    }
  } else if (commerceRaw && !billingRaw) {
    status = 'unknown';
  } else {
    status = 'unknown';
  }

  const result: ReconciliationDiagnostics = {
    billingAccountId,
    tenantId,
    commerceAccessRecommendation:  commerceRec,
    commerceAccountStanding:       commerceRaw ? String(commerceRaw.accountStandingStatus ?? '') : null,
    commerceSnapshotGeneratedAt:   commerceGen,
    commerceSubscriptionCount:     subCount,
    commerceActiveSubscriptions:   activeSubCount,
    billingEntitlementStatus:      billingRaw ? String(billingRaw.entitlementStatus ?? '') : null,
    billingAccessRecommendation:   billingRec,
    billingLastSyncedAt:           billingSync,
    billingEffectiveFrom:          billingRaw ? String(billingRaw.effectiveFromUtc ?? '') || null : null,
    reconciliationStatus:          status,
    mismatchDetails:               mismatch,
    staleDeltaSeconds:             staleDelta,
    staleThresholdSeconds:         STALE_THRESHOLD_SECS,
    lastCheckedAtUtc:              new Date().toISOString(),
    error:                         null,
    commerceError:                 commerceErr,
    billingError:                  billingErr,
  };

  return NextResponse.json(result);
}
