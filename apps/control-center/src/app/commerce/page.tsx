import { cookies } from 'next/headers';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { CCShell } from '@/components/shell/cc-shell';
import { CommerceServiceCard, CommerceReadinessPanel } from '@/components/commerce/commerce-service-card';
import type { CommerceSummary } from '@/types/control-center';

export const dynamic = 'force-dynamic';

async function fetchCommerceSummary(): Promise<CommerceSummary> {
  const base = process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');
  const res = await fetch(`${base}/api/commerce/summary`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`Commerce summary failed: ${res.status}`);
  return res.json();
}

export default async function CommercePage() {
  const session = await requirePlatformAdmin();

  let data:       CommerceSummary | null = null;
  let fetchError: string | null         = null;

  try {
    data = await fetchCommerceSummary();
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Failed to load Commerce data.';
  }

  return (
    <CCShell userEmail={session.email}>
      <div className="min-h-full bg-gray-50">
        <div className="max-w-5xl mx-auto px-6 py-8">

          <div className="mb-6">
            <div className="flex items-center gap-3">
              <h1 className="text-xl font-semibold text-gray-900">Commerce</h1>
              <span className="inline-flex items-center text-[11px] font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-700">
                IN PROGRESS
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Platform billing service health and operational status.
            </p>
          </div>

          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load Commerce data</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : data ? (
            <div className="space-y-5">
              <CommerceServiceCard
                status={data.serviceStatus}
                latencyMs={data.serviceLatencyMs}
                checkedAt={data.lastCheckedAtUtc}
              />

              {data.readinessChecks.length > 0 && (
                <CommerceReadinessPanel checks={data.readinessChecks} />
              )}

              <div className="bg-white rounded-xl border border-gray-200 px-6 py-5">
                <h2 className="text-sm font-semibold text-gray-700 mb-3">Service Information</h2>
                <dl className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Domain</dt>
                    <dd className="font-medium text-gray-900">Platform / SaaS Billing</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Port</dt>
                    <dd className="font-mono text-gray-700">5030</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Gateway Route</dt>
                    <dd className="font-mono text-gray-700">/commerce/**</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Health Endpoint</dt>
                    <dd className="font-mono text-gray-700">/health</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Readiness Endpoint</dt>
                    <dd className="font-mono text-gray-700">/ready</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Integration Mode</dt>
                    <dd className="font-medium text-gray-700">Standalone (LegalSynq:Identity:Enabled=false)</dd>
                  </div>
                </dl>
                <p className="text-xs text-gray-400 mt-4 border-t border-gray-100 pt-3">
                  Commerce handles platform-level subscription billing (Platform → Tenant).
                  It is separate from Tenant Billing which handles tenant → customer invoicing.
                  Detailed billing account management is out of scope for this view.
                </p>
              </div>
            </div>
          ) : null}

        </div>
      </div>
    </CCShell>
  );
}
