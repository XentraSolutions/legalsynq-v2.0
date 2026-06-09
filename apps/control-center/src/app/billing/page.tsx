import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { BillingServiceCard } from '@/components/billing/billing-service-card';
import type { BillingSummary } from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchBillingSummary(): Promise<BillingSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const res = await fetch(`${base}/api/billing/summary`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Billing summary failed: ${res.status}`);
  return res.json();
}

export default async function BillingPage() {
  const session = await requirePlatformAdmin();

  let data:       BillingSummary | null = null;
  let fetchError: string | null        = null;

  try {
    data = await fetchBillingSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load Tenant Billing data.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-5xl mx-auto px-6 py-8">

          <div className="mb-6">
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Tenant Billing</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                IN PROGRESS
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Tenant Billing service health and operational status.
            </p>
          </div>

          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load Tenant Billing data</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : data ? (
            <div className="space-y-5">
              <BillingServiceCard
                status={data.serviceStatus}
                latencyMs={data.serviceLatencyMs}
                checkedAt={data.lastCheckedAtUtc}
              />

              <div className="bg-white rounded-xl border border-gray-200 px-6 py-5">
                <h2 className="text-sm font-semibold text-gray-700 mb-3">Service Information</h2>
                <dl className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Domain</dt>
                    <dd className="font-medium text-gray-900">Tenant Billing (Tenant → Customer)</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Port</dt>
                    <dd className="font-mono text-gray-700">5031</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Gateway Route</dt>
                    <dd className="font-mono text-gray-700">/billing/**</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Health Endpoints</dt>
                    <dd className="font-mono text-gray-700">/health · /healthz</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">API Gate</dt>
                    <dd className="font-medium text-gray-700">X-Internal-Token (BILLING_INTERNAL_TOKEN)</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Integration Mode</dt>
                    <dd className="font-medium text-gray-700">Standalone (LegalSynq:TenantContext:Enabled=false)</dd>
                  </div>
                </dl>
                <p className="text-xs text-gray-400 mt-4 border-t border-gray-100 pt-3">
                  Tenant Billing handles invoicing, payments, and customer billing on behalf of tenants
                  (Tenant → Customer / Client). It is separate from Commerce which manages platform-level
                  subscription billing (Platform → Tenant). Tenant-specific profiles are visible on each
                  tenant&apos;s detail page.
                </p>
              </div>

              <div className="bg-blue-50 border border-blue-200 rounded-xl px-6 py-4">
                <div className="flex items-start gap-3">
                  <i className="ri-information-line text-blue-500 text-lg shrink-0 mt-0.5" />
                  <div>
                    <p className="text-sm font-medium text-blue-800">Tenant Billing profiles are scoped per tenant</p>
                    <p className="text-xs text-blue-700 mt-1">
                      To view a tenant&apos;s billing profile, navigate to the tenant&apos;s detail page
                      from the Tenants section. Billing profile visibility requires
                      <code className="font-mono bg-blue-100 px-1 rounded mx-1">BILLING_INTERNAL_TOKEN</code>
                      to be configured.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          ) : null}

        </div>
      </div>
    </CCShell>
  );
}
