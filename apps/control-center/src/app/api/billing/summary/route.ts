import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { BILLING_SERVICE_URL } from '@/lib/env';
import type { BillingSummary, BillingServiceStatus } from '@/types/control-center';

export async function GET() {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const start = Date.now();
  let serviceStatus: BillingServiceStatus = 'offline';
  let serviceLatencyMs: number | undefined;

  try {
    const healthRes = await fetch(`${BILLING_SERVICE_URL}/health`, {
      cache: 'no-store',
      signal: AbortSignal.timeout(4000),
    });
    serviceLatencyMs = Date.now() - start;

    if (healthRes.ok) {
      serviceStatus = serviceLatencyMs > 2000 ? 'degraded' : 'online';
    } else {
      serviceStatus = 'degraded';
    }
  } catch {
    serviceLatencyMs = Date.now() - start;
    serviceStatus = 'offline';
  }

  if (serviceStatus !== 'offline') {
    try {
      const healthzRes = await fetch(`${BILLING_SERVICE_URL}/healthz`, {
        cache: 'no-store',
        signal: AbortSignal.timeout(4000),
      });
      if (!healthzRes.ok && serviceStatus === 'online') {
        serviceStatus = 'degraded';
      }
    } catch {
      if (serviceStatus === 'online') serviceStatus = 'degraded';
    }
  }

  const summary: BillingSummary = {
    serviceStatus,
    serviceLatencyMs,
    lastCheckedAtUtc: new Date().toISOString(),
  };

  return NextResponse.json(summary, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
