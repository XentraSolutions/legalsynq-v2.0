import { NextResponse }         from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL } from '@/lib/env';
import type { CommerceBridgeDiagnostics } from '@/types/control-center';

export const dynamic = 'force-dynamic';

function offline(error: string): CommerceBridgeDiagnostics {
  return {
    enabled:                 false,
    baseUrlConfigured:       false,
    internalTokenConfigured: false,
    timeoutSeconds:          0,
    retryAttempts:           0,
    circuitBreakerEnabled:   false,
    circuitBreakerState:     'unknown',
    targetRoute:             '',
    mode:                    'unknown',
    autoPublishEnabled:      false,
    autoPublishQueueDepth:   0,
    outboxEnabled:           false,
    outboxPendingCount:      0,
    outboxFailedCount:       0,
    outboxPublishedCount:    0,
    lastCheckedAtUtc:        new Date().toISOString(),
    error,
  };
}

/**
 * GET /api/commerce/bridge-diagnostics
 *
 * Calls the Commerce service TB-INT-02 diagnostics endpoint
 * (GET /api/commerce/integration/tenant-billing/diagnostics) and returns a
 * safe operational summary. Never exposes internal tokens or secrets —
 * the Commerce endpoint already guarantees this (InternalTokenConfigured is
 * a boolean presence flag, not the token value).
 *
 * Access: PlatformAdmin only.
 */
export async function GET() {
  await requirePlatformAdmin();

  try {
    const res = await fetch(
      `${COMMERCE_SERVICE_URL}/api/commerce/integration/tenant-billing/diagnostics`,
      {
        headers: { Accept: 'application/json' },
        signal: AbortSignal.timeout(5000),
      },
    );

    if (!res.ok) {
      if (res.status === 401 || res.status === 403) {
        return NextResponse.json(
          offline('Commerce identity integration not enabled or unauthorized.'),
          { headers: { 'Cache-Control': 'no-store' } },
        );
      }
      return NextResponse.json(
        offline(`Commerce service returned HTTP ${res.status}.`),
        { headers: { 'Cache-Control': 'no-store' } },
      );
    }

    const d = await res.json() as Record<string, unknown>;

    const payload: CommerceBridgeDiagnostics = {
      enabled:                 Boolean(d['enabled'] ?? false),
      baseUrlConfigured:       Boolean(d['baseUrlConfigured'] ?? false),
      internalTokenConfigured: Boolean(d['internalTokenConfigured'] ?? false),
      timeoutSeconds:          Number(d['timeoutSeconds'] ?? 0),
      retryAttempts:           Number(d['retryAttempts'] ?? 0),
      circuitBreakerEnabled:   Boolean(d['circuitBreakerEnabled'] ?? false),
      circuitBreakerState:     String(d['circuitBreakerState'] ?? 'unknown'),
      targetRoute:             String(d['targetRoute'] ?? ''),
      mode:                    String(d['mode'] ?? 'unknown'),
      autoPublishEnabled:      Boolean(d['autoPublishEnabled'] ?? false),
      autoPublishQueueDepth:   Number(d['autoPublishQueueDepth'] ?? 0),
      outboxEnabled:           Boolean(d['outboxEnabled'] ?? false),
      outboxPendingCount:      Number(d['outboxPendingCount'] ?? 0),
      outboxFailedCount:       Number(d['outboxFailedCount'] ?? 0),
      outboxPublishedCount:    Number(d['outboxPublishedCount'] ?? 0),
      lastCheckedAtUtc:        new Date().toISOString(),
      error:                   null,
    };

    return NextResponse.json(payload, { headers: { 'Cache-Control': 'no-store' } });
  } catch {
    return NextResponse.json(
      offline('Commerce service unreachable.'),
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }
}
