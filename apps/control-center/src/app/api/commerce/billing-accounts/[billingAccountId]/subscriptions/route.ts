import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import type { CommerceSubscriptionSummary, CommerceSubscriptionItem } from '@/types/control-center';

const COMMERCE_URL = () => process.env.COMMERCE_SERVICE_URL ?? 'http://127.0.0.1:5030';
const UUID_RE      = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function empty(billingAccountId: string, error: string): CommerceSubscriptionSummary {
  return {
    subscriptions:    [],
    totalCount:       0,
    billingAccountId,
    lastCheckedAtUtc: new Date().toISOString(),
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
      `${COMMERCE_URL()}/api/commerce/billing-accounts/${billingAccountId}/subscriptions`,
      { cache: 'no-store' },
    );

    if (upstream.status === 401 || upstream.status === 403) {
      return NextResponse.json(
        empty(billingAccountId, 'Commerce identity integration not enabled (standalone mode).'),
        { status: 200 },
      );
    }

    if (!upstream.ok) {
      return NextResponse.json(
        empty(billingAccountId, `Commerce returned HTTP ${upstream.status}.`),
        { status: 200 },
      );
    }

    const raw: unknown[] = await upstream.json();
    const subscriptions: CommerceSubscriptionItem[] = (Array.isArray(raw) ? raw : []).map(s => {
      const sub = s as Record<string, unknown>;
      return {
        id:                    String(sub.id ?? ''),
        billingAccountId:      String(sub.billingAccountId ?? billingAccountId),
        subscriptionNumber:    String(sub.subscriptionNumber ?? ''),
        status:                String(sub.status ?? 'Unknown'),
        startDateUtc:          String(sub.startDateUtc ?? ''),
        currentPeriodStartUtc: String(sub.currentPeriodStartUtc ?? ''),
        currentPeriodEndUtc:   String(sub.currentPeriodEndUtc ?? ''),
        cancelAtPeriodEnd:     Boolean(sub.cancelAtPeriodEnd),
        cancelledAtUtc:        sub.cancelledAtUtc != null ? String(sub.cancelledAtUtc) : null,
        cancellationReason:    sub.cancellationReason != null ? String(sub.cancellationReason) : null,
        createdAtUtc:          String(sub.createdAtUtc ?? ''),
        updatedAtUtc:          String(sub.updatedAtUtc ?? ''),
        itemCount:             Array.isArray(sub.items) ? sub.items.length : 0,
      };
    });

    return NextResponse.json({
      subscriptions,
      totalCount:       subscriptions.length,
      billingAccountId,
      lastCheckedAtUtc: new Date().toISOString(),
      error:            null,
    } satisfies CommerceSubscriptionSummary);

  } catch {
    return NextResponse.json(
      empty(billingAccountId, 'Commerce service unreachable.'),
      { status: 200 },
    );
  }
}
