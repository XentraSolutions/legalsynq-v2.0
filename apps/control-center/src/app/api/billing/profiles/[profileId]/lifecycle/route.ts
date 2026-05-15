import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { BILLING_SERVICE_URL, BILLING_INTERNAL_TOKEN } from '@/lib/env';
import type { BillingProfileLifecycle, BillingProfileLifecycleEvent } from '@/types/control-center';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function empty(profileId: string, error: string): BillingProfileLifecycle {
  return {
    profileId,
    tenantId:         '',
    billingAccountId: '',
    currentStatus:    'Unknown',
    mode:             '',
    events:           [],
    updatedAtUtc:     '',
    lastCheckedAtUtc: new Date().toISOString(),
    error,
  };
}

function deriveEvents(profile: Record<string, unknown>): BillingProfileLifecycleEvent[] {
  const events: BillingProfileLifecycleEvent[] = [];

  if (profile.createdAtUtc) {
    events.push({
      event:       'Created',
      status:      'Draft',
      occurredAtUtc: String(profile.createdAtUtc),
      notes:       'Profile created in Draft status.',
    });
  }

  if (profile.activatedAtUtc) {
    events.push({
      event:       'Activated',
      status:      'Active',
      occurredAtUtc: String(profile.activatedAtUtc),
      notes:       null,
    });
  }

  const status = String(profile.status ?? '').toLowerCase();

  if (status === 'suspended' && !profile.activatedAtUtc) {
    events.push({
      event:       'Suspended',
      status:      'Suspended',
      occurredAtUtc: String(profile.updatedAtUtc ?? ''),
      notes:       'Suspended without prior activation. Exact suspension timestamp not persisted — updatedAtUtc shown.',
    });
  } else if (status === 'suspended' && profile.activatedAtUtc) {
    events.push({
      event:       'Suspended',
      status:      'Suspended',
      occurredAtUtc: String(profile.updatedAtUtc ?? ''),
      notes:       'Exact suspension timestamp not persisted — updatedAtUtc shown.',
    });
  }

  if (profile.closedAtUtc) {
    events.push({
      event:       'Closed',
      status:      'Closed',
      occurredAtUtc: String(profile.closedAtUtc),
      notes:       'Terminal state. Profile is permanently closed.',
    });
  }

  events.sort((a, b) => a.occurredAtUtc.localeCompare(b.occurredAtUtc));
  return events;
}

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ profileId: string }> },
) {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { profileId } = await params;

  if (!UUID_RE.test(profileId)) {
    return NextResponse.json(
      empty(profileId, 'Invalid profileId — must be a UUID.'),
      { status: 400 },
    );
  }

  const token = BILLING_INTERNAL_TOKEN;
  if (!token) {
    return NextResponse.json(
      empty(profileId, 'Billing internal token not configured — BILLING_INTERNAL_TOKEN is missing.'),
      { status: 200 },
    );
  }

  try {
    const upstream = await fetch(
      `${BILLING_SERVICE_URL}/api/tenant-billing/profiles/${profileId}`,
      {
        cache: 'no-store',
        signal: AbortSignal.timeout(6000),
        headers: {
          'X-Internal-Token': token,
          Accept: 'application/json',
        },
      },
    );

    if (upstream.status === 404) {
      return NextResponse.json(
        empty(profileId, 'Profile not found.'),
        { status: 200 },
      );
    }

    if (!upstream.ok) {
      return NextResponse.json(
        empty(profileId, `Billing service returned HTTP ${upstream.status}.`),
        { status: 200 },
      );
    }

    const profile = await upstream.json() as Record<string, unknown>;
    const events  = deriveEvents(profile);

    const result: BillingProfileLifecycle = {
      profileId:        String(profile.id ?? profileId),
      tenantId:         String(profile.tenantId ?? ''),
      billingAccountId: String(profile.billingAccountId ?? ''),
      currentStatus:    String(profile.status ?? 'Unknown'),
      mode:             String(profile.mode ?? ''),
      events,
      updatedAtUtc:     String(profile.updatedAtUtc ?? ''),
      lastCheckedAtUtc: new Date().toISOString(),
      error:            null,
    };

    return NextResponse.json(result);

  } catch {
    return NextResponse.json(
      empty(profileId, 'Billing service unreachable.'),
      { status: 200 },
    );
  }
}
