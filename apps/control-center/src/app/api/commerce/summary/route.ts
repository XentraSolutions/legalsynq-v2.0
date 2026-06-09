import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL } from '@/lib/env';
import type { CommerceSummary, CommerceServiceStatus, CommerceReadinessCheck } from '@/types/control-center';

export async function GET() {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const start = Date.now();
  let serviceStatus: CommerceServiceStatus = 'offline';
  let serviceLatencyMs: number | undefined;
  const readinessChecks: CommerceReadinessCheck[] = [];

  try {
    const healthRes = await fetch(`${COMMERCE_SERVICE_URL}/health`, {
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
      const readyRes = await fetch(`${COMMERCE_SERVICE_URL}/ready`, {
        cache: 'no-store',
        signal: AbortSignal.timeout(6000),
      });
      const body = await readyRes.json().catch(() => ({}));

      const dbStatus = typeof body.database === 'string' ? body.database : undefined;
      if (dbStatus) {
        readinessChecks.push({
          name:   'database',
          status: dbStatus === 'ok' ? 'ok' : dbStatus === 'not-configured' ? 'degraded' : 'error',
        });
      }

      if (!readyRes.ok && serviceStatus === 'online') {
        serviceStatus = 'degraded';
      }
    } catch {
      readinessChecks.push({ name: 'readiness_probe', status: 'error' });
      if (serviceStatus === 'online') serviceStatus = 'degraded';
    }
  }

  const summary: CommerceSummary = {
    serviceStatus,
    serviceLatencyMs,
    lastCheckedAtUtc: new Date().toISOString(),
    readinessChecks,
  };

  return NextResponse.json(summary, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
