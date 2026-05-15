import { cookies } from 'next/headers';
import { requirePlatformAdmin }              from '@/lib/auth-guards';
import { CCShell }                           from '@/components/shell/cc-shell';
import { CommerceServiceCard, CommerceReadinessPanel } from '@/components/commerce/commerce-service-card';
import { CommerceBridgeDiagnosticsPanel }    from '@/components/commerce/commerce-bridge-diagnostics-panel';
import { CommerceAccountPanel }              from '@/components/commerce/commerce-account-panel';
import type {
  CommerceSummary,
  CommerceBridgeDiagnostics,
  CommerceAccountSummary,
} from '@/types/control-center';

export const dynamic = 'force-dynamic';

const BASE = () => process.env.CONTROL_CENTER_SELF_URL ?? 'http://127.0.0.1:5004';

async function bffFetch<T>(path: string, cookieHeader: string): Promise<T> {
  const res = await fetch(`${BASE()}${path}`, {
    cache: 'no-store',
    headers: { cookie: cookieHeader },
  });
  if (!res.ok) throw new Error(`${path} failed: ${res.status}`);
  return res.json();
}

export default async function CommercePage() {
  const session = await requirePlatformAdmin();

  const cookieStore  = await cookies();
  const cookieHeader = cookieStore.getAll().map(c => `${c.name}=${c.value}`).join('; ');

  let summary:     CommerceSummary      | null = null;
  let diagnostics: CommerceBridgeDiagnostics | null = null;
  let accounts:    CommerceAccountSummary    | null = null;
  let fetchError:  string | null             = null;

  const [summaryResult, diagnosticsResult, accountsResult] = await Promise.allSettled([
    bffFetch<CommerceSummary>('/api/commerce/summary', cookieHeader),
    bffFetch<CommerceBridgeDiagnostics>('/api/commerce/bridge-diagnostics', cookieHeader),
    bffFetch<CommerceAccountSummary>('/api/commerce/account-detail', cookieHeader),
  ]);

  if (summaryResult.status === 'fulfilled') {
    summary = summaryResult.value;
  } else {
    fetchError = summaryResult.reason instanceof Error
      ? summaryResult.reason.message
      : 'Failed to load Commerce data.';
  }
  if (diagnosticsResult.status === 'fulfilled') diagnostics = diagnosticsResult.value;
  if (accountsResult.status === 'fulfilled')    accounts    = accountsResult.value;

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
              Platform billing service health, billing accounts, and bridge diagnostics.
            </p>
          </div>

          {fetchError ? (
            <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4">
              <p className="text-sm text-red-700 font-medium">Failed to load Commerce data</p>
              <p className="text-xs text-red-600 mt-1">{fetchError}</p>
            </div>
          ) : (
            <div className="space-y-5">
              {summary && (
                <CommerceServiceCard
                  status={summary.serviceStatus}
                  latencyMs={summary.serviceLatencyMs}
                  checkedAt={summary.lastCheckedAtUtc}
                />
              )}

              {summary && summary.readinessChecks.length > 0 && (
                <CommerceReadinessPanel checks={summary.readinessChecks} />
              )}

              <CommerceBridgeDiagnosticsPanel
                diagnostics={diagnostics}
                error={diagnostics?.error ?? (diagnosticsResult.status === 'rejected' ? 'Bridge diagnostics unavailable.' : null)}
              />

              <CommerceAccountPanel
                summary={accounts}
                error={accountsResult.status === 'rejected' ? 'Billing account data unavailable.' : null}
              />

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
                    <dt className="text-xs text-gray-500 mb-1">Health / Readiness</dt>
                    <dd className="font-mono text-gray-700">/health · /ready</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Bridge Diagnostics</dt>
                    <dd className="font-mono text-gray-700">/api/commerce/integration/tenant-billing/diagnostics</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-gray-500 mb-1">Integration Mode</dt>
                    <dd className="font-medium text-gray-700">Standalone (LegalSynq:Identity:Enabled=false by default)</dd>
                  </div>
                </dl>
                <p className="text-xs text-gray-400 mt-4 border-t border-gray-100 pt-3">
                  Commerce handles platform-level SaaS billing (Platform → Tenant).
                  Tenant Billing handles tenant → customer invoicing and is a separate service.
                  Billing account management and subscription editing are out of scope for this view.
                </p>
              </div>
            </div>
          )}

        </div>
      </div>
    </CCShell>
  );
}
