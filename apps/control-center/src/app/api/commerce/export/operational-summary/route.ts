import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL, BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';

export async function GET() {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const token = BILLING_INTERNAL_TOKEN;
  const now   = new Date().toISOString();

  const [diagnosticsResult, accountsResult] = await Promise.allSettled([
    fetch(`${COMMERCE_SERVICE_URL}/api/commerce/integration/tenant-billing/diagnostics`, {
      cache: 'no-store', signal: AbortSignal.timeout(6000),
    }),
    fetch(`${COMMERCE_SERVICE_URL}/api/commerce/admin/dashboard/summary`, {
      cache: 'no-store', signal: AbortSignal.timeout(6000),
    }),
  ]);

  let bridgeDiagnostics: unknown = null;
  let adminSummary: unknown = null;
  let billingHealth: unknown = null;

  if (diagnosticsResult.status === 'fulfilled' && diagnosticsResult.value.ok) {
    try { bridgeDiagnostics = await diagnosticsResult.value.json(); } catch { /* ignore */ }
  }

  if (accountsResult.status === 'fulfilled' && accountsResult.value.ok) {
    try { adminSummary = await accountsResult.value.json(); } catch { /* ignore */ }
  }

  if (token) {
    try {
      const hr = await fetch(`${BILLING_SERVICE_URL}/healthz`, {
        cache: 'no-store', signal: AbortSignal.timeout(4000),
      });
      billingHealth = { status: hr.ok ? 'ok' : 'degraded', httpStatus: hr.status };
    } catch {
      billingHealth = { status: 'unreachable' };
    }
  } else {
    billingHealth = { status: 'token-not-configured' };
  }

  const payload = {
    exportedAtUtc:       now,
    exportFormat:        'json',
    exportVersion:       '1',
    ticketId:            'LS-COMMERCE-OPS-01',
    sections: {
      commerceBridgeDiagnostics: bridgeDiagnostics,
      commerceAdminSummary:      adminSummary,
      billingServiceHealth:      billingHealth,
    },
    metadata: {
      note: 'Read-only operational export. Contains no payment data, secrets, or connection strings.',
      generatedBy: 'LegalSynq Control Center',
    },
  };

  const json   = JSON.stringify(payload, null, 2);
  const date   = now.slice(0, 10).replace(/-/g, '');
  const filename = `legalsynq-ops-${date}.json`;

  return new NextResponse(json, {
    status: 200,
    headers: {
      'Content-Type':        'application/json',
      'Content-Disposition': `attachment; filename="${filename}"`,
      'Cache-Control':       'no-store',
    },
  });
}
