'use client';

import type { BillingEntitlementSnapshot } from '@/types/control-center';

interface Props {
  snapshot:  BillingEntitlementSnapshot | null;
  error?:    string | null;
  tenantId?: string;
}

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active')    return 'bg-emerald-100 text-emerald-800';
  if (s === 'suspended') return 'bg-amber-100  text-amber-800';
  if (s === 'blocked')   return 'bg-red-100    text-red-800';
  if (s === 'none')      return 'bg-slate-100  text-slate-600';
  return 'bg-slate-100 text-slate-500';
}

function recColor(rec: string): string {
  const r = rec.toLowerCase();
  if (r === 'allow')    return 'bg-emerald-100 text-emerald-800';
  if (r === 'readonly') return 'bg-amber-100  text-amber-800';
  if (r === 'deny')     return 'bg-red-100    text-red-800';
  return 'bg-slate-100 text-slate-500';
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4 py-1.5 border-b border-slate-100 last:border-0 text-sm">
      <span className="text-slate-500 shrink-0">{label}</span>
      <span className="text-slate-800 font-medium text-right">{children}</span>
    </div>
  );
}

function Pill({ value, colorClass }: { value: string; colorClass: string }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${colorClass}`}>
      {value}
    </span>
  );
}

function BoolFlag({ value }: { value: boolean }) {
  return value
    ? <span className="text-emerald-600 font-semibold">Yes</span>
    : <span className="text-slate-400">No</span>;
}

function fmt(iso: string | null | undefined): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

export function BillingEntitlementPanel({ snapshot, error, tenantId }: Props) {
  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2">
        <i className="ri-shield-check-line text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">Entitlement Status</h2>
        {tenantId && (
          <span className="ml-auto text-xs text-slate-400 font-mono">{tenantId}</span>
        )}
      </div>

      <div className="p-5">
        {error && (
          <div className="flex items-start gap-2 rounded-md bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-800 mb-4">
            <i className="ri-error-warning-line mt-0.5 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {!snapshot && !error && (
          <div className="flex items-center gap-2 text-sm text-slate-400 py-4">
            <i className="ri-information-line" />
            No entitlement data available for this tenant.
          </div>
        )}

        {snapshot && (
          <div className="space-y-0.5">
            <Row label="Entitlement Status">
              <Pill value={snapshot.entitlementStatus} colorClass={statusColor(snapshot.entitlementStatus)} />
            </Row>
            <Row label="Access Recommendation">
              <Pill value={snapshot.accessRecommendation} colorClass={recColor(snapshot.accessRecommendation)} />
            </Row>
            <Row label="Platform Access Enabled">
              <BoolFlag value={snapshot.isEnabled} />
            </Row>
            <Row label="Write Access Allowed">
              <BoolFlag value={snapshot.writeAccessAllowed} />
            </Row>
            {snapshot.sourcePlanKey && (
              <Row label="Source Plan">{snapshot.sourcePlanKey}</Row>
            )}
            {snapshot.sourceProductKey && (
              <Row label="Source Product">{snapshot.sourceProductKey}</Row>
            )}
            {snapshot.billingAccountId && (
              <Row label="Billing Account ID">
                <span className="font-mono text-xs">{snapshot.billingAccountId}</span>
              </Row>
            )}
            <Row label="Effective From">{fmt(snapshot.effectiveFromUtc)}</Row>
            <Row label="Last Synced">{fmt(snapshot.lastSyncedAtUtc)}</Row>
            <Row label="Last Checked">{fmt(snapshot.lastCheckedAtUtc)}</Row>
          </div>
        )}
      </div>
    </section>
  );
}
