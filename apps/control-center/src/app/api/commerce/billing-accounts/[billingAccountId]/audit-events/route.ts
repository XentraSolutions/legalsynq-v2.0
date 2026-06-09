import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { COMMERCE_SERVICE_URL } from '@/lib/env';
import type { CommerceAuditEventList, CommerceAuditEvent } from '@/types/control-center';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function empty(billingAccountId: string, error: string): CommerceAuditEventList {
  return {
    events:          [],
    totalCount:      0,
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
      `${COMMERCE_SERVICE_URL}/api/commerce/billing-accounts/${billingAccountId}/audit-events`,
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
        empty(billingAccountId, 'Billing account not found.'),
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
    const events: CommerceAuditEvent[] = (Array.isArray(raw) ? raw : []).map(e => {
      const ev = e as Record<string, unknown>;
      return {
        id:              String(ev.id ?? ''),
        billingAccountId: String(ev.billingAccountId ?? billingAccountId),
        eventType:       String(ev.eventType ?? 'Unknown'),
        description:     String(ev.description ?? ''),
        actorType:       String(ev.actorType ?? 'Unknown'),
        actorId:         ev.actorId != null ? String(ev.actorId) : null,
        metadataJson:    ev.metadataJson != null ? String(ev.metadataJson) : null,
        createdAtUtc:    String(ev.createdAtUtc ?? ''),
      };
    });

    return NextResponse.json({
      events,
      totalCount:      events.length,
      billingAccountId,
      lastCheckedAtUtc: new Date().toISOString(),
      error:           null,
    } satisfies CommerceAuditEventList);

  } catch {
    return NextResponse.json(
      empty(billingAccountId, 'Commerce service unreachable.'),
      { status: 200 },
    );
  }
}
