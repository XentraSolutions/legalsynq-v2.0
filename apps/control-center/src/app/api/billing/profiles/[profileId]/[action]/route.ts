import { NextRequest, NextResponse } from 'next/server';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { cookies } from 'next/headers';
import type { BillingProfileActionResult } from '@/types/control-center';

const ALLOWED_ACTIONS = new Set(['activate', 'suspend', 'close']);
const UUID_RE         = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const BILLING_URL     = () => process.env.BILLING_SERVICE_URL ?? 'http://127.0.0.1:5031';
const BILLING_TOKEN   = () => process.env.BILLING_INTERNAL_TOKEN ?? '';

function errResult(action: string, profileId: string, error: string): BillingProfileActionResult {
  return { success: false, action, profileId, error, executedAtUtc: new Date().toISOString() };
}

export async function POST(
  _req: NextRequest,
  { params }: { params: Promise<{ profileId: string; action: string }> },
) {
  try {
    await requirePlatformAdmin();
  } catch {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { profileId, action } = await params;

  if (!UUID_RE.test(profileId)) {
    return NextResponse.json(
      errResult(action, profileId, 'Invalid profileId — must be a UUID.'),
      { status: 400 },
    );
  }

  if (!ALLOWED_ACTIONS.has(action)) {
    return NextResponse.json(
      errResult(action, profileId, `Unknown action "${action}". Allowed: activate, suspend, close.`),
      { status: 400 },
    );
  }

  const token = BILLING_TOKEN();
  if (!token) {
    return NextResponse.json(
      errResult(action, profileId, 'Billing internal token not configured — BILLING_INTERNAL_TOKEN is missing.'),
      { status: 503 },
    );
  }

  const cookieStore  = await cookies();
  const sessionToken = cookieStore.get('platform_session')?.value ?? '';

  const tenantId = await resolveTenantId(sessionToken);
  if (!tenantId) {
    return NextResponse.json(
      errResult(action, profileId, 'Unable to resolve tenant ID from session.'),
      { status: 400 },
    );
  }

  try {
    const upstream = await fetch(
      `${BILLING_URL()}/api/tenant-billing/profiles/${profileId}/${action}`,
      {
        method:  'POST',
        headers: {
          'X-Tenant-Id':     tenantId,
          'X-Internal-Token': token,
          'Content-Type':    'application/json',
        },
      },
    );

    if (upstream.status === 404) {
      return NextResponse.json(
        errResult(action, profileId, `Profile ${profileId} not found.`),
        { status: 404 },
      );
    }

    if (upstream.status === 409) {
      let detail = 'Transition not allowed for the current profile state.';
      try {
        const body = await upstream.json();
        if (body?.detail) detail = body.detail;
      } catch { /* ignore */ }
      return NextResponse.json(errResult(action, profileId, detail), { status: 409 });
    }

    if (!upstream.ok) {
      return NextResponse.json(
        errResult(action, profileId, `Billing service returned HTTP ${upstream.status}.`),
        { status: upstream.status },
      );
    }

    const updated = await upstream.json();
    const result: BillingProfileActionResult = {
      success:       true,
      action,
      profileId,
      newStatus:     updated?.status ?? undefined,
      executedAtUtc: new Date().toISOString(),
    };
    return NextResponse.json(result);

  } catch {
    return NextResponse.json(
      errResult(action, profileId, 'Billing service unreachable.'),
      { status: 503 },
    );
  }
}

async function resolveTenantId(sessionToken: string): Promise<string | null> {
  if (!sessionToken) return null;
  try {
    const base = process.env.CONTROL_CENTER_API_BASE ?? 'http://127.0.0.1:5010';
    const res  = await fetch(`${base}/identity/api/auth/me`, {
      headers: { Authorization: `Bearer ${sessionToken}` },
      cache:   'no-store',
    });
    if (!res.ok) return null;
    const me = await res.json();
    return typeof me.tenantId === 'string' ? me.tenantId : null;
  } catch {
    return null;
  }
}
