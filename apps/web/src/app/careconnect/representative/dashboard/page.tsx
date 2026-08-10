'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { fetchRepresentativeMetrics } from '@/lib/representative-portal-api';
import { useRepresentativePortal } from '@/components/careconnect/representative-access-code-gate';
import { ApiError } from '@/lib/api-client';
import type { RepresentativeReferralMetrics } from '@/types/careconnect';

/** Matches the authenticated CareConnect dashboard's stat-card style — plain bordered box, bold number, gray label. */
function StatCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-gray-200 p-5">
      <p className="text-2xl font-bold text-gray-900">{value.toLocaleString()}</p>
      <p className="mt-1 text-sm text-gray-500">{label}</p>
    </div>
  );
}

export default function RepresentativeDashboardPage() {
  const { code } = useRepresentativePortal();
  const [from, setFrom] = useState('');
  const [to, setTo]     = useState('');
  const [metrics, setMetrics] = useState<RepresentativeReferralMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    fetchRepresentativeMetrics(code, from || undefined, to || undefined)
      .then(({ data }) => { setMetrics(data); setError(null); })
      .catch(err => setError(err instanceof ApiError ? err.message : 'Failed to load dashboard metrics.'))
      .finally(() => setLoading(false));
  }, [code, from, to]);

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Metrics for referrals attributed to you</p>
        </div>
      </div>

      <div className="flex items-end gap-3 mb-6">
        <div>
          <label className="block text-xs font-medium text-gray-700 mb-1">Submitted from</label>
          <input type="date" value={from} onChange={e => setFrom(e.target.value)} className="border border-gray-300 rounded-md px-3 py-2 text-sm" />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-700 mb-1">Submitted to</label>
          <input type="date" value={to} onChange={e => setTo(e.target.value)} className="border border-gray-300 rounded-md px-3 py-2 text-sm" />
        </div>
        {(from || to) && (
          <button onClick={() => { setFrom(''); setTo(''); }} className="text-sm text-gray-500 hover:text-gray-900 pb-2 cursor-pointer">
            Clear
          </button>
        )}
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 mb-6">{error}</div>
      )}

      {loading ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : metrics && metrics.totalAttributedReferrals === 0 ? (
        <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
          <h3 className="text-base font-semibold text-gray-900 mb-1">No referrals yet</h3>
          <p className="text-sm text-gray-500">
            {from || to
              ? 'No referrals match the selected date range.'
              : "No referrals have been submitted for this source yet — they'll appear here once they are."}
          </p>
        </div>
      ) : metrics ? (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
          <StatCard label="Total Attributed" value={metrics.totalAttributedReferrals} />
          <StatCard label="Pending" value={metrics.pendingReferrals} />
          <StatCard label="Accepted" value={metrics.acceptedReferrals} />
          <StatCard label="Declined" value={metrics.declinedReferrals} />
          <StatCard label="Completed" value={metrics.completedReferrals} />
          <StatCard label="Cancelled" value={metrics.cancelledReferrals} />
        </div>
      ) : null}
    </div>
  );
}
