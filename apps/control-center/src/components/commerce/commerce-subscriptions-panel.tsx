'use client';

import { useState, useEffect, useCallback } from 'react';
import type { CommerceSubscriptionItem } from '@/types/control-center';

interface Props {
  billingAccountId: string;
  accountName?:     string;
}

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active')   return 'bg-emerald-100 text-emerald-800';
  if (s === 'trialing') return 'bg-blue-100    text-blue-800';
  if (s === 'paused')   return 'bg-amber-100   text-amber-800';
  if (s === 'cancelled' || s === 'canceled') return 'bg-red-100 text-red-800';
  if (s === 'expired')  return 'bg-slate-100   text-slate-500';
  return 'bg-slate-100 text-slate-500';
}

function fmt(iso: string | null | undefined): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }); }
  catch { return iso; }
}

function SubscriptionRow({ sub }: { sub: CommerceSubscriptionItem }) {
  return (
    <div className="py-3 border-b border-slate-100 last:border-0">
      <div className="flex items-center gap-3 mb-1">
        <span className="text-sm font-medium text-slate-800 font-mono">{sub.subscriptionNumber}</span>
        <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold ${statusColor(sub.status)}`}>
          {sub.status}
        </span>
        {sub.cancelAtPeriodEnd && (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-orange-100 text-orange-800">
            Cancels at period end
          </span>
        )}
      </div>
      <div className="grid grid-cols-2 gap-x-6 gap-y-0.5 text-xs text-slate-500 mt-1">
        <span>Period: {fmt(sub.currentPeriodStartUtc)} — {fmt(sub.currentPeriodEndUtc)}</span>
        <span>Started: {fmt(sub.startDateUtc)}</span>
        {sub.cancelledAtUtc && <span className="text-red-600">Cancelled: {fmt(sub.cancelledAtUtc)}</span>}
        {sub.cancellationReason && <span className="text-red-500 col-span-2">Reason: {sub.cancellationReason}</span>}
        <span>{sub.itemCount} line item{sub.itemCount !== 1 ? 's' : ''}</span>
      </div>
    </div>
  );
}

export function CommerceSubscriptionsPanel({ billingAccountId, accountName }: Props) {
  const [subscriptions, setSubscriptions] = useState<CommerceSubscriptionItem[] | null>(null);
  const [error,         setError]         = useState<string | null>(null);
  const [loading,       setLoading]       = useState(false);
  const [expanded,      setExpanded]      = useState(false);

  const load = useCallback(async () => {
    if (loading) return;
    setLoading(true);
    setError(null);
    try {
      const res  = await fetch(
        `/api/commerce/billing-accounts/${encodeURIComponent(billingAccountId)}/subscriptions`,
        { credentials: 'include' },
      );
      const data = await res.json();
      if (data.error) {
        setError(data.error);
        setSubscriptions([]);
      } else {
        setSubscriptions(data.subscriptions ?? []);
      }
    } catch {
      setError('Unable to load subscriptions.');
    } finally {
      setLoading(false);
    }
  }, [billingAccountId, loading]);

  useEffect(() => {
    if (expanded && subscriptions === null) {
      load();
    }
  }, [expanded, subscriptions, load]);

  return (
    <div className="border border-slate-200 rounded-lg overflow-hidden bg-white">
      <button
        type="button"
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center gap-3 px-5 py-3 bg-slate-50 hover:bg-slate-100 transition-colors text-left"
      >
        <i className="ri-loop-right-line text-indigo-500" />
        <span className="text-sm font-semibold text-slate-700 flex-1">
          Subscriptions{accountName ? ` — ${accountName}` : ''}
        </span>
        {loading && <i className="ri-loader-4-line animate-spin text-slate-400" />}
        {!loading && subscriptions !== null && (
          <span className="text-xs text-slate-500">
            {subscriptions.length} subscription{subscriptions.length !== 1 ? 's' : ''}
          </span>
        )}
        <i className={`ri-arrow-${expanded ? 'up' : 'down'}-s-line text-slate-400`} />
      </button>

      {expanded && (
        <div className="px-5 py-4">
          {error && (
            <div className="flex items-start gap-2 rounded-md bg-amber-50 border border-amber-200 px-4 py-3 text-sm text-amber-800">
              <i className="ri-information-line mt-0.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {loading && !subscriptions && (
            <div className="flex items-center gap-2 text-sm text-slate-400 py-3">
              <i className="ri-loader-4-line animate-spin" />
              Loading subscriptions…
            </div>
          )}

          {!loading && subscriptions !== null && subscriptions.length === 0 && !error && (
            <div className="flex items-center gap-2 text-sm text-slate-400 py-3">
              <i className="ri-inbox-line" />
              No subscriptions found for this billing account.
            </div>
          )}

          {subscriptions && subscriptions.length > 0 && (
            <div className="divide-y divide-slate-100">
              {subscriptions.map(sub => (
                <SubscriptionRow key={sub.id} sub={sub} />
              ))}
            </div>
          )}

          <button
            type="button"
            onClick={load}
            disabled={loading}
            className="mt-3 text-xs text-indigo-500 hover:text-indigo-700 disabled:opacity-50 transition-colors"
          >
            {loading ? 'Refreshing…' : 'Refresh'}
          </button>
        </div>
      )}
    </div>
  );
}
