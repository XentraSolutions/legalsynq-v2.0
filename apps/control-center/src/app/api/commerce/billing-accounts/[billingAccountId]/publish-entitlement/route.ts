import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import type { EntitlementPublishResult } from '@/types/control-center';

const COMMERCE_URL = () => process.env.COMMERCE_SERVICE_URL ?? 'http://127.0.0.1:5030';
const UUID_RE      = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function errResult(billingAccountId: string, error: string): EntitlementPublishResult {
  return {
    outcome:          'failed',
    billingAccountId,
    tenantId:         null,
    httpStatus:       null,
    reason:           error,
    attempts:         0,
    executedAtUtc:    new Date().toISOString(),
    error,
  };
}

export async function POST(
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

  try {
    const upstream = await fetch(
      `${COMMERCE_URL()}/api/commerce/integration/tenant-billing/billing-accounts/${billingAccountId}/publish-entitlement`,
      {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
      },
    );

    if (upstream.status === 401 || upstream.status === 403) {
      return NextResponse.json(
        errResult(billingAccountId, 'Commerce identity integration not enabled. Set LegalSynq:Identity:Enabled=true on Commerce to use this operation.'),
        { status: 503 },
      );
    }

    if (upstream.status === 404) {
      return NextResponse.json(
        errResult(billingAccountId, `Billing account ${billingAccountId} not found in Commerce.`),
        { status: 404 },
      );
    }

    if (!upstream.ok) {
      return NextResponse.json(
        errResult(billingAccountId, `Commerce returned HTTP ${upstream.status}.`),
        { status: upstream.status },
      );
    }

    const body = await upstream.json();
    const result: EntitlementPublishResult = {
      outcome:          body.outcome         ?? 'unknown',
      billingAccountId: String(body.billingAccountId ?? billingAccountId),
      tenantId:         body.tenantId        ?? null,
      httpStatus:       body.httpStatus      ?? null,
      reason:           body.reason          ?? '',
      attempts:         body.attempts        ?? 1,
      executedAtUtc:    new Date().toISOString(),
    };
    return NextResponse.json(result);

  } catch {
    return NextResponse.json(
      errResult(billingAccountId, 'Commerce service unreachable.'),
      { status: 503 },
    );
  }
}
