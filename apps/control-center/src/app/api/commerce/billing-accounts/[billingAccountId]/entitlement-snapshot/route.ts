import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL } from '@/lib/env';
import type { CommerceEntitlementSnapshotDetail } from '@/types/control-center';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function empty(billingAccountId: string, error: string): CommerceEntitlementSnapshotDetail {
  return {
    billingAccountId,
    accountNumber:                      '',
    displayName:                        '',
    hostPlatformKey:                    null,
    externalTenantId:                   null,
    accountStandingStatus:              'Unknown',
    accountStandingReason:              null,
    accountStandingGracePeriodEndsAtUtc: null,
    accessRecommendation:               'Unknown',
    productCount:                       0,
    planCount:                          0,
    subscriptionCount:                  0,
    activeSubscriptionCount:            0,
    featureLimitCount:                  0,
    products:                           [],
    plans:                              [],
    generatedAtUtc:                     '',
    lastCheckedAtUtc:                   new Date().toISOString(),
    error,
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
      empty(billingAccountId, 'Invalid billingAccountId — must be a UUID.'),
      { status: 400 },
    );
  }

  try {
    const upstream = await fetch(
      `${COMMERCE_SERVICE_URL}/api/commerce/integration/billing-accounts/${billingAccountId}/entitlement-snapshot`,
      { cache: 'no-store', signal: AbortSignal.timeout(6000) },
    );

    if (upstream.status === 401 || upstream.status === 403) {
      return NextResponse.json(
        empty(billingAccountId, 'Commerce identity integration not enabled (standalone mode).'),
        { status: 200 },
      );
    }

    if (upstream.status === 404) {
      return NextResponse.json(
        empty(billingAccountId, 'No entitlement snapshot found for this billing account.'),
        { status: 200 },
      );
    }

    if (!upstream.ok) {
      return NextResponse.json(
        empty(billingAccountId, `Commerce returned HTTP ${upstream.status}.`),
        { status: 200 },
      );
    }

    const raw = await upstream.json() as Record<string, unknown>;

    const products = Array.isArray(raw.products) ? raw.products as Array<Record<string, unknown>> : [];
    const plans    = Array.isArray(raw.plans)    ? raw.plans    as Array<Record<string, unknown>> : [];
    const subs     = Array.isArray(raw.subscriptions) ? raw.subscriptions as Array<Record<string, unknown>> : [];
    const limits   = Array.isArray(raw.limits)   ? raw.limits   as Array<Record<string, unknown>> : [];

    const activeSubCount = subs.filter(s => String(s.status ?? '').toLowerCase() === 'active').length;

    const result: CommerceEntitlementSnapshotDetail = {
      billingAccountId:                   String(raw.billingAccountId ?? billingAccountId),
      accountNumber:                      String(raw.accountNumber ?? ''),
      displayName:                        String(raw.displayName ?? ''),
      hostPlatformKey:                    raw.hostPlatformKey != null ? String(raw.hostPlatformKey) : null,
      externalTenantId:                   raw.externalTenantId != null ? String(raw.externalTenantId) : null,
      accountStandingStatus:              String(raw.accountStandingStatus ?? 'Unknown'),
      accountStandingReason:              raw.accountStandingReason != null ? String(raw.accountStandingReason) : null,
      accountStandingGracePeriodEndsAtUtc: raw.accountStandingGracePeriodEndsAtUtc != null ? String(raw.accountStandingGracePeriodEndsAtUtc) : null,
      accessRecommendation:               String(raw.accessRecommendation ?? 'Unknown'),
      productCount:                       products.length,
      planCount:                          plans.length,
      subscriptionCount:                  subs.length,
      activeSubscriptionCount:            activeSubCount,
      featureLimitCount:                  limits.length,
      products:  products.map(p => ({ productKey: String(p.productKey ?? ''), productName: String(p.productName ?? '') })),
      plans:     plans.map(p => ({ planKey: String(p.planKey ?? ''), planName: String(p.planName ?? '') })),
      generatedAtUtc:   String(raw.generatedAtUtc ?? ''),
      lastCheckedAtUtc: new Date().toISOString(),
      error:            null,
    };

    return NextResponse.json(result);

  } catch {
    return NextResponse.json(
      empty(billingAccountId, 'Commerce service unreachable.'),
      { status: 200 },
    );
  }
}
