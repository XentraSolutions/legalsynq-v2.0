import { cookies }                    from 'next/headers';
import { requireAdmin }                from '@/lib/auth-guards';
import { TenantBillingPanel }          from '@/components/billing/tenant-billing-panel';
import { BillingEntitlementPanel }     from '@/components/billing/billing-entitlement-panel';
import type { TenantAdminBillingStatus } from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchMyBillingStatus(tenantId?: string): Promise<TenantAdminBillingStatus> {
  const base        = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const qs  = tenantId ? `?tenantId=${encodeURIComponent(tenantId)}` : '';
  const res = await fetch(`${base}/api/billing/my-billing-status${qs}`, {
    cache:   'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Billing status failed: ${res.status}`);
  return res.json();
}

/**
 * /billing-status
 *
 * TenantAdmin-accessible page showing the calling user's own tenant billing
 * profile + entitlement status.
 *
 * - TenantAdmin: always sees their own tenant (enforced in the BFF route)
 * - PlatformAdmin: sees their own session tenant by default; use ?tenantId=
 *   in the URL to inspect a specific tenant (operational convenience)
 *
 * Access: PlatformAdmin OR TenantAdmin (requireAdmin)
 */
export default async function BillingStatusPage({
  searchParams,
}: {
  searchParams: Promise<{ tenantId?: string }>;
}) {
  const session = await requireAdmin();
  const { tenantId: qTenant } = await searchParams;

  const tenantId = session.isPlatformAdmin && qTenant ? qTenant : session.tenantId;

  let data: TenantAdminBillingStatus | null = null;
  let fetchError: string | null = null;

  try {
    data = await fetchMyBillingStatus(session.isPlatformAdmin ? tenantId : undefined);
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load billing status.';
  }

  const displayTenantId = data?.tenantId ?? tenantId ?? '—';

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-semibold text-slate-800">Billing Status</h1>
        <p className="text-sm text-slate-500 mt-1">
          {session.isPlatformAdmin
            ? 'Operational billing profile and entitlement status for this tenant.'
            : 'Your organisation\'s billing profile and entitlement status.'}
        </p>
      </div>

      {fetchError && (
        <div className="flex items-start gap-2 rounded-md bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mt-0.5 shrink-0" />
          <span>{fetchError}</span>
        </div>
      )}

      {data && (
        <>
          <TenantBillingPanel
            summary={{
              profileFound:     data.profileFound,
              profile:          data.profile,
              lastCheckedAtUtc: data.lastCheckedAtUtc,
              error:            data.error,
            }}
            error={null}
          />

          <BillingEntitlementPanel
            snapshot={data.entitlement}
            error={data.entitlement?.error ?? null}
            tenantId={displayTenantId ?? undefined}
          />
        </>
      )}

      <div className="text-xs text-slate-400">
        Tenant ID: <span className="font-mono">{displayTenantId}</span>
      </div>
    </div>
  );
}
