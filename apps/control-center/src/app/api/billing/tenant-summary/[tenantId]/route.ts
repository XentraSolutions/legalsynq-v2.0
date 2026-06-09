import { NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';
import type { TenantBillingSummary } from '@/types/control-center';

interface RouteContext {
  params: Promise<{ tenantId: string }>;
}

export async function GET(_req: Request, ctx: RouteContext) {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { tenantId } = await ctx.params;

  if (!tenantId || !/^[0-9a-f-]{36}$/i.test(tenantId)) {
    return NextResponse.json({ error: 'Invalid tenantId' }, { status: 400 });
  }

  if (!BILLING_INTERNAL_TOKEN) {
    const summary: TenantBillingSummary = {
      profileFound:    false,
      profile:         null,
      lastCheckedAtUtc: new Date().toISOString(),
      error:           'Billing service internal token not configured — tenant billing data unavailable.',
    };
    return NextResponse.json(summary, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  }

  try {
    const res = await fetch(`${BILLING_SERVICE_URL}/api/tenant-billing/profiles?page=1&pageSize=1`, {
      cache: 'no-store',
      signal: AbortSignal.timeout(6000),
      headers: {
        'X-Internal-Token': BILLING_INTERNAL_TOKEN,
        'X-Tenant-Id':      tenantId,
        'Accept':           'application/json',
      },
    });

    if (res.status === 401 || res.status === 403) {
      const summary: TenantBillingSummary = {
        profileFound:    false,
        profile:         null,
        lastCheckedAtUtc: new Date().toISOString(),
        error:           'Billing service access denied — verify BILLING_INTERNAL_TOKEN is correct.',
      };
      return NextResponse.json(summary, {
        headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
      });
    }

    if (!res.ok) {
      const summary: TenantBillingSummary = {
        profileFound:    false,
        profile:         null,
        lastCheckedAtUtc: new Date().toISOString(),
        error:           `Billing service returned HTTP ${res.status}.`,
      };
      return NextResponse.json(summary, {
        headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
      });
    }

    const body = await res.json();
    const items: unknown[] = Array.isArray(body.items) ? body.items : Array.isArray(body) ? body : [];
    const profile = items.length > 0 ? (items[0] as Record<string, unknown>) : null;

    const summary: TenantBillingSummary = {
      profileFound:    profile !== null,
      profile: profile ? {
        id:               String(profile.id ?? ''),
        tenantId:         String(profile.tenantId ?? ''),
        billingAccountId: String(profile.billingAccountId ?? ''),
        hostPlatformKey:  profile.hostPlatformKey ? String(profile.hostPlatformKey) : null,
        externalTenantId: profile.externalTenantId ? String(profile.externalTenantId) : null,
        status:           String(profile.status ?? ''),
        mode:             String(profile.mode ?? ''),
        createdAtUtc:     String(profile.createdAtUtc ?? ''),
        updatedAtUtc:     String(profile.updatedAtUtc ?? ''),
        activatedAtUtc:   profile.activatedAtUtc ? String(profile.activatedAtUtc) : null,
        closedAtUtc:      profile.closedAtUtc ? String(profile.closedAtUtc) : null,
      } : null,
      lastCheckedAtUtc: new Date().toISOString(),
      error:           null,
    };

    return NextResponse.json(summary, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  } catch (err) {
    const summary: TenantBillingSummary = {
      profileFound:    false,
      profile:         null,
      lastCheckedAtUtc: new Date().toISOString(),
      error:           'Tenant Billing service unreachable.',
    };
    void err;
    return NextResponse.json(summary, {
      headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
    });
  }
}
