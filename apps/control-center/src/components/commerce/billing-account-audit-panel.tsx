'use client';

import { useState } from 'react';
import type { CommerceAuditEventList, CommerceAuditEvent } from '@/types/control-center';

interface Props {
  billingAccountId: string;
  accountName:      string;
}

function actorBadge(actorType: string): string {
  const t = actorType.toLowerCase();
  if (t === 'admin')        return 'bg-indigo-100 text-indigo-700';
  if (t === 'system')       return 'bg-slate-100 text-slate-600';
  if (t === 'hostplatform') return 'bg-blue-100 text-blue-700';
  return 'bg-slate-100 text-slate-500';
}

function fmt(iso: string): string {
  if (!iso) return '—';
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

function EventRow({ ev }: { ev: CommerceAuditEvent }) {
  const [showMeta, setShowMeta] = useState(false);
  return (
    <div className="py-2.5 border-b border-slate-100 last:border-0">
      <div className="flex items-start gap-3">
        <div className="mt-0.5 shrink-0">
          <i className="ri-file-list-3-line text-slate-400 text-sm" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-medium text-slate-700 font-mono">{ev.eventType}</span>
            <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded ${actorBadge(ev.actorType)}`}>
              {ev.actorType}
            </span>
            {ev.actorId && (
              <span className="text-[10px] text-slate-400 font-mono">{ev.actorId}</span>
            )}
          </div>
          {ev.description && (
            <p className="text-xs text-slate-600 mt-0.5">{ev.description}</p>
          )}
          <div className="flex items-center gap-3 mt-1">
            <span className="text-[10px] text-slate-400">{fmt(ev.createdAtUtc)}</span>
            {ev.metadataJson && (
              <button
                onClick={() => setShowMeta(v => !v)}
                className="text-[10px] text-indigo-500 hover:text-indigo-700 underline"
              >
                {showMeta ? 'Hide metadata' : 'View metadata'}
              </button>
            )}
          </div>
          {showMeta && ev.metadataJson && (
            <pre className="mt-1.5 text-[10px] bg-slate-50 border border-slate-200 rounded p-2 overflow-x-auto text-slate-600 max-h-32">
              {(() => {
                try { return JSON.stringify(JSON.parse(ev.metadataJson), null, 2); }
                catch { return ev.metadataJson; }
              })()}
            </pre>
          )}
        </div>
      </div>
    </div>
  );
}

export function BillingAccountAuditPanel({ billingAccountId, accountName }: Props) {
  const [data,    setData]    = useState<CommerceAuditEventList | null>(null);
  const [loading, setLoading] = useState(false);
  const [loaded,  setLoaded]  = useState(false);

  async function load() {
    if (loading) return;
    setLoading(true);
    try {
      const res = await fetch(
        `/api/commerce/billing-accounts/${encodeURIComponent(billingAccountId)}/audit-events`,
        { credentials: 'include' },
      );
      const json: CommerceAuditEventList = await res.json();
      setData(json);
    } catch {
      setData({
        events: [], totalCount: 0, billingAccountId,
        lastCheckedAtUtc: new Date().toISOString(),
        error: 'Failed to load audit events.',
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
            <i className="ri-file-list-3-line" />
            <span>Audit events for <strong>{accountName}</strong></span>
          </div>
          <button
            onClick={load}
            className="text-xs px-3 py-1 rounded bg-slate-200 text-slate-700 hover:bg-slate-300 transition-colors"
          >
            Load
          </button>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="bg-slate-50 border border-slate-200 rounded-md px-4 py-3 flex items-center gap-2 text-xs text-slate-400">
        <i className="ri-loader-4-line animate-spin" />
        Loading audit events…
      </div>
    );
  }

  return (
    <div className="bg-slate-50 border border-slate-200 rounded-md overflow-hidden">
      <div className="px-4 py-2.5 border-b border-slate-200 flex items-center gap-2 bg-white">
        <i className="ri-file-list-3-line text-slate-400" />
        <span className="text-xs font-semibold text-slate-600">
          Audit Events — {accountName}
        </span>
        <span className="ml-auto text-[10px] text-slate-400">
          {data?.totalCount ?? 0} event{(data?.totalCount ?? 0) !== 1 ? 's' : ''}
        </span>
        <button
          onClick={load}
          className="text-[10px] text-slate-400 hover:text-slate-600 transition-colors"
          title="Refresh"
        >
          <i className="ri-refresh-line" />
        </button>
      </div>

      <div className="px-4">
        {data?.error && (
          <div className="flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-md px-3 py-2 my-3 text-xs text-amber-800">
            <i className="ri-information-line mt-0.5 shrink-0" />
            <span>{data.error}</span>
          </div>
        )}

        {!data?.error && data?.events.length === 0 && (
          <div className="flex items-center gap-2 text-xs text-slate-400 py-4">
            <i className="ri-inbox-line" />
            No audit events found for this account.
          </div>
        )}

        {data?.events.map(ev => <EventRow key={ev.id} ev={ev} />)}
      </div>

      {data && !data.error && (
        <div className="px-4 py-1.5 border-t border-slate-100 text-[10px] text-slate-400">
          Last checked: {fmt(data.lastCheckedAtUtc)}
        </div>
      )}
    </div>
  );
}
