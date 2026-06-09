'use client';

import { useState } from 'react';
import type { BillingProfileLifecycle, BillingProfileLifecycleEvent } from '@/types/control-center';

interface Props {
  profileId: string;
}

function statusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active')    return 'bg-emerald-100 text-emerald-800 border-emerald-200';
  if (s === 'suspended') return 'bg-amber-100  text-amber-800  border-amber-200';
  if (s === 'closed')    return 'bg-slate-100  text-slate-500  border-slate-200';
  if (s === 'draft')     return 'bg-blue-50    text-blue-600   border-blue-200';
  return 'bg-slate-100 text-slate-500 border-slate-200';
}

function eventIcon(event: string): string {
  const e = event.toLowerCase();
  if (e === 'created')   return 'ri-add-circle-line text-blue-500';
  if (e === 'activated') return 'ri-checkbox-circle-line text-emerald-500';
  if (e === 'suspended') return 'ri-pause-circle-line text-amber-500';
  if (e === 'closed')    return 'ri-close-circle-line text-slate-400';
  return 'ri-time-line text-slate-400';
}

function fmt(iso: string | null): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

function TimelineEvent({ ev, isLast }: { ev: BillingProfileLifecycleEvent; isLast: boolean }) {
  return (
    <div className="flex gap-3">
      <div className="flex flex-col items-center">
        <div className={`w-6 h-6 rounded-full border-2 flex items-center justify-center shrink-0 ${statusColor(ev.status)}`}>
          <i className={`${eventIcon(ev.event)} text-xs`} />
        </div>
        {!isLast && <div className="w-px flex-1 bg-slate-200 mt-1 mb-0.5" />}
      </div>
      <div className="pb-4 flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm font-medium text-slate-700">{ev.event}</span>
          <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border ${statusColor(ev.status)}`}>
            {ev.status}
          </span>
        </div>
        <div className="text-xs text-slate-400 mt-0.5">{fmt(ev.occurredAtUtc)}</div>
        {ev.notes && (
          <p className="text-xs text-slate-500 mt-1 italic">{ev.notes}</p>
        )}
      </div>
    </div>
  );
}

export function BillingProfileLifecyclePanel({ profileId }: Props) {
  const [data,    setData]    = useState<BillingProfileLifecycle | null>(null);
  const [loading, setLoading] = useState(false);
  const [loaded,  setLoaded]  = useState(false);
  const [open,    setOpen]    = useState(false);

  async function load() {
    if (loading) return;
    setLoading(true);
    try {
      const res = await fetch(
        `/api/billing/profiles/${encodeURIComponent(profileId)}/lifecycle`,
        { credentials: 'include' },
      );
      setData(await res.json() as BillingProfileLifecycle);
    } catch {
      setData({
        profileId, tenantId: '', billingAccountId: '', currentStatus: 'Unknown',
        mode: '', events: [], updatedAtUtc: '',
        lastCheckedAtUtc: new Date().toISOString(),
        error: 'Failed to load lifecycle data.',
      });
    } finally {
      setLoading(false);
      setLoaded(true);
      setOpen(true);
    }
  }

  function toggle() {
    if (!loaded) { load(); return; }
    setOpen(v => !v);
  }

  return (
    <section className="bg-white border border-slate-200 rounded-lg overflow-hidden">
      <button
        onClick={toggle}
        className="w-full px-5 py-3 bg-slate-50 border-b border-slate-200 flex items-center gap-2 text-left hover:bg-slate-100 transition-colors"
      >
        <i className="ri-timeline-line text-indigo-500" />
        <h2 className="text-sm font-semibold text-slate-700">Billing Profile Lifecycle History</h2>
        {data && !data.error && (
          <span className={`ml-2 text-[10px] font-semibold px-2 py-0.5 rounded border ${statusColor(data.currentStatus)}`}>
            {data.currentStatus}
          </span>
        )}
        <div className="ml-auto flex items-center gap-2">
          {loading && <i className="ri-loader-4-line animate-spin text-slate-400 text-sm" />}
          <i className={`${open ? 'ri-arrow-up-s-line' : 'ri-arrow-down-s-line'} text-slate-400`} />
        </div>
      </button>

      {open && (
        <div className="p-5">
          {!loaded && !loading && (
            <div className="text-sm text-slate-400 flex items-center gap-2">
              <i className="ri-loader-4-line animate-spin" /> Loading…
            </div>
          )}

          {data?.error && (
            <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-md px-4 py-3 text-sm text-amber-800">
              <i className="ri-information-line mt-0.5 shrink-0" />
              <span>{data.error}</span>
            </div>
          )}

          {data && !data.error && data.events.length === 0 && (
            <div className="text-sm text-slate-400 flex items-center gap-2">
              <i className="ri-inbox-line" /> No lifecycle events found.
            </div>
          )}

          {data && !data.error && data.events.length > 0 && (
            <div className="space-y-0">
              {data.events.map((ev, idx) => (
                <TimelineEvent
                  key={`${ev.event}-${ev.occurredAtUtc}`}
                  ev={ev}
                  isLast={idx === data.events.length - 1}
                />
              ))}
            </div>
          )}

          {data && !data.error && (
            <div className="mt-4 pt-3 border-t border-slate-100">
              <dl className="grid grid-cols-2 gap-x-6 gap-y-1.5">
                <div>
                  <dt className="text-xs text-slate-400">Profile ID</dt>
                  <dd className="text-xs font-mono text-slate-600 truncate">{data.profileId}</dd>
                </div>
                <div>
                  <dt className="text-xs text-slate-400">Mode</dt>
                  <dd className="text-xs text-slate-700">{data.mode || '—'}</dd>
                </div>
                <div>
                  <dt className="text-xs text-slate-400">Billing Account ID</dt>
                  <dd className="text-xs font-mono text-slate-600 truncate">{data.billingAccountId || '—'}</dd>
                </div>
                <div>
                  <dt className="text-xs text-slate-400">Last Updated</dt>
                  <dd className="text-xs text-slate-500">{fmt(data.updatedAtUtc)}</dd>
                </div>
              </dl>
              <div className="mt-2 text-xs text-slate-400">
                Lifecycle note: Only created, activated, and closed timestamps are persisted. Suspension uses
                <code className="mx-1 px-1 bg-slate-100 rounded font-mono">updatedAtUtc</code> as a proxy timestamp.
              </div>
            </div>
          )}

          {data && (
            <div className="mt-2 text-xs text-slate-400">
              Last checked: {fmt(data.lastCheckedAtUtc)}
              {!data.error && (
                <button
                  onClick={load}
                  className="ml-3 text-indigo-500 hover:text-indigo-700 underline"
                >
                  Refresh
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
