'use client';

import { useState } from 'react';
import type { ReconciliationDiagnostics, ReconciliationStatus } from '@/types/control-center';

interface Props {
  billingAccountId: string;
  accountName:      string;
}

function statusConfig(s: ReconciliationStatus): { label: string; icon: string; card: string; badge: string } {
  switch (s) {
    case 'aligned':  return { label: 'Aligned',  icon: 'ri-checkbox-circle-line', card: 'bg-emerald-50 border-emerald-200', badge: 'bg-emerald-100 text-emerald-800' };
    case 'stale':    return { label: 'Stale',     icon: 'ri-time-line',            card: 'bg-amber-50  border-amber-200',   badge: 'bg-amber-100  text-amber-800'  };
    case 'mismatch': return { label: 'Mismatch',  icon: 'ri-error-warning-line',   card: 'bg-red-50    border-red-200',     badge: 'bg-red-100    text-red-800'    };
    case 'unknown':  return { label: 'Unknown',   icon: 'ri-question-line',        card: 'bg-slate-50  border-slate-200',   badge: 'bg-slate-100  text-slate-500'  };
    case 'error':    return { label: 'Error',     icon: 'ri-close-circle-line',    card: 'bg-red-50    border-red-200',     badge: 'bg-red-100    text-red-800'    };
    default:         return { label: 'Unknown',   icon: 'ri-question-line',        card: 'bg-slate-50  border-slate-200',   badge: 'bg-slate-100  text-slate-500'  };
  }
}

function recColor(rec: string | null): string {
  if (!rec) return 'text-slate-400';
  const r = rec.toLowerCase();
  if (r === 'allow')       return 'text-emerald-700';
  if (r === 'readonly')    return 'text-amber-700';
  if (r === 'gracelimited')return 'text-orange-700';
  if (r === 'block')       return 'text-red-700';
  return 'text-slate-600';
}

function fmt(iso: string | null): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

function formatStaleDelta(secs: number | null): string {
  if (secs === null) return '—';
  if (secs < 60)     return `${secs}s`;
  if (secs < 3600)   return `${Math.round(secs / 60)}m`;
  if (secs < 86400)  return `${Math.round(secs / 3600)}h`;
  return `${Math.round(secs / 86400)}d`;
}

export function EntitlementReconciliationPanel({ billingAccountId, accountName }: Props) {
  const [data,    setData]    = useState<ReconciliationDiagnostics | null>(null);
  const [loading, setLoading] = useState(false);
  const [loaded,  setLoaded]  = useState(false);

  async function load() {
    if (loading) return;
    setLoading(true);
    try {
      const res = await fetch(
        `/api/commerce/reconciliation/${encodeURIComponent(billingAccountId)}`,
        { credentials: 'include' },
      );
      setData(await res.json() as ReconciliationDiagnostics);
    } catch {
      setData({
        billingAccountId,
        tenantId:                     null,
        commerceAccessRecommendation: null,
        commerceAccountStanding:      null,
        commerceSnapshotGeneratedAt:  null,
        commerceSubscriptionCount:    null,
        commerceActiveSubscriptions:  null,
        billingEntitlementStatus:     null,
        billingAccessRecommendation:  null,
        billingLastSyncedAt:          null,
        billingEffectiveFrom:         null,
        reconciliationStatus:         'error',
        mismatchDetails:              null,
        staleDeltaSeconds:            null,
        staleThresholdSeconds:        86400,
        lastCheckedAtUtc:             new Date().toISOString(),
        error:                        'Failed to load reconciliation data.',
        commerceError:                null,
        billingError:                 null,
      });
    } finally {
      setLoading(false);
      setLoaded(true);
    }
  }

  if (!loaded && !loading) {
    return (
      <div className="bg-slate-50 border border-slate-200 rounded-md px-4 py-3">
        <div className="flex items-center gap-2 justify-between">
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <i className="ri-git-merge-line" />
            <span>Reconciliation for <strong>{accountName}</strong></span>
          </div>
          <button
            onClick={load}
            className="text-xs px-3 py-1 rounded bg-slate-200 text-slate-700 hover:bg-slate-300 transition-colors"
          >
            Run
          </button>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="bg-slate-50 border border-slate-200 rounded-md px-4 py-3 flex items-center gap-2 text-xs text-slate-400">
        <i className="ri-loader-4-line animate-spin" />
        Running reconciliation check…
      </div>
    );
  }

  const cfg = statusConfig(data!.reconciliationStatus);

  return (
    <div className={`border rounded-md overflow-hidden ${cfg.card}`}>
      <div className="px-4 py-2.5 border-b border-inherit flex items-center gap-2 bg-white/70">
        <i className={`${cfg.icon} text-sm`} />
        <span className="text-xs font-semibold text-slate-700">Reconciliation — {accountName}</span>
        <span className={`ml-2 text-[10px] font-semibold px-2 py-0.5 rounded ${cfg.badge}`}>
          {cfg.label}
        </span>
        <button
          onClick={load}
          className="ml-auto text-[10px] text-slate-400 hover:text-slate-600"
          title="Refresh"
        >
          <i className="ri-refresh-line" />
        </button>
      </div>

      <div className="px-4 py-3 space-y-3">
        {data?.error && (
          <div className="text-xs text-red-700 flex items-center gap-2">
            <i className="ri-error-warning-line shrink-0" />
            <span>{data.error}</span>
          </div>
        )}

        {data?.mismatchDetails && (
          <div className="text-xs text-red-700 flex items-start gap-2 bg-red-50 border border-red-200 rounded px-3 py-2">
            <i className="ri-error-warning-line mt-0.5 shrink-0" />
            <span>{data.mismatchDetails}</span>
          </div>
        )}

        {data?.reconciliationStatus === 'stale' && data.staleDeltaSeconds !== null && (
          <div className="text-xs text-amber-700 flex items-start gap-2 bg-amber-50 border border-amber-200 rounded px-3 py-2">
            <i className="ri-time-line mt-0.5 shrink-0" />
            <span>
              Billing snapshot is <strong>{formatStaleDelta(data.staleDeltaSeconds)}</strong> old
              (threshold: {formatStaleDelta(data.staleThresholdSeconds)}). A new publish may be needed.
            </span>
          </div>
        )}

        <div className="grid grid-cols-2 gap-4">
          {/* Commerce side */}
          <div className="space-y-2">
            <div className="text-[10px] font-semibold text-slate-500 uppercase tracking-wide">
              Commerce (Source)
            </div>
            <dl className="space-y-1">
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Access Recommendation</dt>
                <dd className={`text-[10px] font-semibold ${recColor(data?.commerceAccessRecommendation ?? null)}`}>
                  {data?.commerceAccessRecommendation ?? '—'}
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Account Standing</dt>
                <dd className="text-[10px] font-medium text-slate-700">{data?.commerceAccountStanding ?? '—'}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Subscriptions</dt>
                <dd className="text-[10px] text-slate-700">
                  {data?.commerceSubscriptionCount !== null
                    ? `${data?.commerceActiveSubscriptions} active / ${data?.commerceSubscriptionCount} total`
                    : '—'}
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Snapshot Generated</dt>
                <dd className="text-[10px] text-slate-500">{fmt(data?.commerceSnapshotGeneratedAt ?? null)}</dd>
              </div>
              {data?.commerceError && (
                <div className="text-[10px] text-amber-700 bg-amber-50 rounded px-2 py-1 mt-1">{data.commerceError}</div>
              )}
            </dl>
          </div>

          {/* Billing side */}
          <div className="space-y-2">
            <div className="text-[10px] font-semibold text-slate-500 uppercase tracking-wide">
              Tenant Billing (Applied)
            </div>
            <dl className="space-y-1">
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Access Recommendation</dt>
                <dd className={`text-[10px] font-semibold ${recColor(data?.billingAccessRecommendation ?? null)}`}>
                  {data?.billingAccessRecommendation ?? '—'}
                </dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Entitlement Status</dt>
                <dd className="text-[10px] font-medium text-slate-700">{data?.billingEntitlementStatus ?? '—'}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Effective From</dt>
                <dd className="text-[10px] text-slate-500">{fmt(data?.billingEffectiveFrom ?? null)}</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-[10px] text-slate-400">Last Synced</dt>
                <dd className="text-[10px] text-slate-500">{fmt(data?.billingLastSyncedAt ?? null)}</dd>
              </div>
              {data?.billingError && (
                <div className="text-[10px] text-amber-700 bg-amber-50 rounded px-2 py-1 mt-1">{data.billingError}</div>
              )}
            </dl>
          </div>
        </div>

        {data?.staleDeltaSeconds !== null && data?.staleDeltaSeconds !== undefined && (
          <div className="text-[10px] text-slate-400 pt-1 border-t border-slate-100">
            Stale delta: {formatStaleDelta(data.staleDeltaSeconds)} · Threshold: {formatStaleDelta(data.staleThresholdSeconds)} ·
            Tenant: {data.tenantId ?? '—'}
          </div>
        )}
      </div>

      <div className="px-4 py-1.5 border-t border-inherit text-[10px] text-slate-400">
        Last checked: {fmt(data?.lastCheckedAtUtc ?? null)}
      </div>
    </div>
  );
}
