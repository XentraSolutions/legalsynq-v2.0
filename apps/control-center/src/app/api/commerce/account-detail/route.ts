import { NextResponse }         from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL } from '@/lib/env';
import type { CommerceAccountSummary, CommerceAccountItem } from '@/types/control-center';

export const dynamic = 'force-dynamic';

function empty(error: string): CommerceAccountSummary {
  return { accountCount: 0, accounts: [], lastCheckedAtUtc: new Date().toISOString(), error };
}

/**
 * GET /api/commerce/account-detail
 *
 * Returns a safe summary of Commerce billing accounts and their current
 * standing for PlatformAdmin operational visibility.
 *
 * Calls Commerce service directly at COMMERCE_SERVICE_URL:
 *   GET /api/commerce/billing-accounts  → account list (status)
 *   GET /api/commerce/billing-accounts/{id}/account-standing → per-account standing
 *
 * If Commerce has LegalSynq:Identity:Enabled=false (standalone mode), the
 * accounts endpoint may return 401. In that case an informational message
 * is returned rather than an error — this is expected in dev deployments.
 *
 * Access: PlatformAdmin only.
 */
export async function GET() {
  await requirePlatformAdmin();
  const now = new Date().toISOString();

  try {
    const listRes = await fetch(`${COMMERCE_SERVICE_URL}/api/commerce/billing-accounts`, {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(6000),
    });

    if (listRes.status === 401 || listRes.status === 403) {
      return NextResponse.json(
        {
          accountCount:     0,
          accounts:         [] as CommerceAccountItem[],
          lastCheckedAtUtc: now,
          error:            'Commerce identity integration not enabled. Set LegalSynq:Identity:Enabled=true on the Commerce service to enable billing account visibility.',
        } satisfies CommerceAccountSummary,
        { headers: { 'Cache-Control': 'no-store' } },
      );
    }

    if (!listRes.ok) {
      return NextResponse.json(
        empty(`Commerce service returned HTTP ${listRes.status}.`),
        { headers: { 'Cache-Control': 'no-store' } },
      );
    }

    const listBody = await listRes.json() as unknown[];
    const accounts = Array.isArray(listBody) ? listBody : [];

    if (accounts.length === 0) {
      return NextResponse.json(
        { accountCount: 0, accounts: [], lastCheckedAtUtc: now, error: null } satisfies CommerceAccountSummary,
        { headers: { 'Cache-Control': 'no-store' } },
      );
    }

    const standingResults = await Promise.allSettled(
      accounts.slice(0, 20).map(async (acct) => {
        const a = acct as Record<string, unknown>;
        const id = String(a['id'] ?? '');
        try {
          const r = await fetch(
            `${COMMERCE_SERVICE_URL}/api/commerce/billing-accounts/${id}/account-standing`,
            { headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(3000) },
          );
          if (r.ok) return { id, standing: await r.json() as Record<string, unknown> };
        } catch { /* ignore per-account standing failure */ }
        return { id, standing: null };
      }),
    );

    const standingMap = new Map<string, Record<string, unknown>>();
    for (const r of standingResults) {
      if (r.status === 'fulfilled' && r.value.standing) {
        standingMap.set(r.value.id, r.value.standing);
      }
    }

    const items: CommerceAccountItem[] = accounts.slice(0, 20).map((acct) => {
      const a       = acct as Record<string, unknown>;
      const id      = String(a['id'] ?? '');
      const s       = standingMap.get(id);
      return {
        id,
        accountNumber: String(a['accountNumber'] ?? ''),
        displayName:   String(a['displayName'] ?? ''),
        status:        String(a['status'] ?? ''),
        standing:      s ? String(s['status'] ?? 'Unknown') : 'Unknown',
        standingReason:              s ? (String(s['reason'] ?? '') || null) : null,
        standingLastEvaluatedAtUtc:  s ? (s['lastEvaluatedAtUtc'] ? String(s['lastEvaluatedAtUtc']) : null) : null,
      };
    });

    return NextResponse.json(
      { accountCount: accounts.length, accounts: items, lastCheckedAtUtc: now, error: null } satisfies CommerceAccountSummary,
      { headers: { 'Cache-Control': 'no-store' } },
    );
  } catch {
    return NextResponse.json(
      empty('Commerce service unreachable.'),
      { headers: { 'Cache-Control': 'no-store' } },
    );
  }
}
