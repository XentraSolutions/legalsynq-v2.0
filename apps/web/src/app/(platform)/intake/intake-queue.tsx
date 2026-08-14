'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import {
  getIntakeReviewSummary,
  listIntakeReviews,
  type IntakeReviewQueueSummary,
  type IntakeReviewSummary,
} from '@/lib/intake-api';
import { ApiError } from '@/lib/api-client';

type Props = {
  currentUserId?: string | null;
  canManage: boolean;
  canAssign: boolean;
  canComplete: boolean;
};

const EMPTY_SUMMARY: IntakeReviewQueueSummary = {
  pending: 0, assigned: 0, inReview: 0, completedToday: 0, highPriority: 0,
  duplicateReviews: 0, noMatchReviews: 0, conflictedReviews: 0, oldestPendingAt: null,
};

export function IntakeQueue({ canAssign, canComplete, canManage }: Props) {
  const [summary, setSummary] = useState(EMPTY_SUMMARY);
  const [items, setItems] = useState<IntakeReviewSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [disposition, setDisposition] = useState('');
  const [sourceType, setSourceType] = useState('');
  const [unassigned, setUnassigned] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const pageSize = 20;

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const [nextSummary, nextPage] = await Promise.all([
        getIntakeReviewSummary(),
        listIntakeReviews({
          page, pageSize, status, priority, disposition, sourceType,
          unassignedOnly: unassigned,
        }),
      ]);
      setSummary(nextSummary);
      setItems(nextPage.items);
      setTotal(nextPage.totalCount);
    } catch (err) {
      setError(err instanceof ApiError && err.isForbidden
        ? 'Your account does not have permission to view the Intake review queue.'
        : 'The Intake review queue is temporarily unavailable.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, [page, status, priority, disposition, sourceType, unassigned]);
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const activeFilterCount = [status, priority, disposition, sourceType].filter(Boolean).length + (unassigned ? 1 : 0);

  const cards = useMemo(() => [
    { label: 'Needs review', value: summary.pending + summary.assigned + summary.inReview, tone: 'text-amber-700 bg-amber-50 border-amber-200' },
    { label: 'Unassigned', value: summary.pending, tone: 'text-slate-700 bg-slate-50 border-slate-200' },
    { label: 'High priority', value: summary.highPriority, tone: 'text-red-700 bg-red-50 border-red-200' },
    { label: 'Completed today', value: summary.completedToday, tone: 'text-emerald-700 bg-emerald-50 border-emerald-200' },
  ], [summary]);

  return (
    <div className="min-h-full bg-slate-50 -m-6 p-6">
      <div className="mx-auto max-w-7xl space-y-5">
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-[0.16em] text-indigo-600">
              <span className="h-1.5 w-1.5 rounded-full bg-indigo-500" />
              Synq Intake
            </div>
            <h1 className="text-2xl font-semibold tracking-tight text-slate-950">Intake Center</h1>
            <p className="mt-1 max-w-2xl text-sm text-slate-500">
              Resolve classification, evidence, matching, and duplicate findings before downstream work begins.
            </p>
          </div>
          <button
            type="button"
            onClick={() => void load()}
            className="inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-sm font-medium text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-slate-50"
          >
            <i className="ri-refresh-line" /> Refresh
          </button>
        </header>

        <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {cards.map((card) => (
            <div key={card.label} className={`rounded-xl border p-4 ${card.tone}`}>
              <div className="text-xs font-medium uppercase tracking-wide opacity-75">{card.label}</div>
              <div className="mt-2 text-2xl font-semibold">{card.value.toLocaleString()}</div>
            </div>
          ))}
        </section>

        <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-slate-100 p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-sm font-semibold text-slate-900">Review queue</h2>
                <p className="mt-0.5 text-xs text-slate-500">
                  {total.toLocaleString()} review{total === 1 ? '' : 's'} · {activeFilterCount ? `${activeFilterCount} filter${activeFilterCount === 1 ? '' : 's'}` : 'all current work'}
                </p>
              </div>
              <div className="flex items-center gap-2 text-xs text-slate-500">
                {summary.oldestPendingAt && <span>Oldest pending {formatAge(summary.oldestPendingAt)}</span>}
                {canComplete && <span className="rounded-full bg-indigo-50 px-2 py-1 font-medium text-indigo-700">Reviewers enabled</span>}
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <select aria-label="Status" value={status} onChange={(e) => { setPage(1); setStatus(e.target.value); }} className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700">
                <option value="">All statuses</option><option value="PENDING">Pending</option><option value="ASSIGNED">Assigned</option><option value="IN_REVIEW">In review</option><option value="COMPLETED">Completed</option>
              </select>
              <select aria-label="Priority" value={priority} onChange={(e) => { setPage(1); setPriority(e.target.value); }} className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700">
                <option value="">All priorities</option><option value="URGENT">Urgent</option><option value="HIGH">High</option><option value="NORMAL">Normal</option><option value="LOW">Low</option>
              </select>
              <select aria-label="Disposition" value={disposition} onChange={(e) => { setPage(1); setDisposition(e.target.value); }} className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700">
                <option value="">All dispositions</option><option value="DUPLICATE">Duplicate</option><option value="CONFLICTED">Conflicted</option><option value="NO_MATCH">No match</option><option value="REVIEW_REQUIRED">Review required</option>
              </select>
              <select aria-label="Source" value={sourceType} onChange={(e) => { setPage(1); setSourceType(e.target.value); }} className="h-9 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700">
                <option value="">All sources</option><option value="EMAIL">Email</option><option value="MANUAL">Manual</option>
              </select>
              <label className="inline-flex h-9 items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm text-slate-600">
                <input type="checkbox" checked={unassigned} onChange={(e) => { setPage(1); setUnassigned(e.target.checked); }} className="rounded border-slate-300 text-indigo-600 focus:ring-indigo-500" />
                Unassigned only
              </label>
              {activeFilterCount > 0 && (
                <button type="button" onClick={() => { setPage(1); setStatus(''); setPriority(''); setDisposition(''); setSourceType(''); setUnassigned(false); }} className="ml-auto text-xs font-medium text-indigo-600 hover:text-indigo-800">Clear filters</button>
              )}
            </div>
          </div>

          {error && <div className="m-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
          {loading ? (
            <div className="space-y-3 p-6"><div className="h-10 animate-pulse rounded bg-slate-100" /><div className="h-10 animate-pulse rounded bg-slate-100" /><div className="h-10 animate-pulse rounded bg-slate-100" /></div>
          ) : items.length === 0 ? (
            <div className="px-6 py-16 text-center"><i className="ri-inbox-line text-3xl text-slate-300" /><p className="mt-3 text-sm font-medium text-slate-600">No reviews match this queue.</p><p className="mt-1 text-xs text-slate-400">New artifacts that require human adjudication will appear here.</p></div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-100 text-sm">
                <thead className="bg-slate-50 text-left text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                  <tr><th className="px-4 py-3">Artifact</th><th className="px-4 py-3">Classification</th><th className="px-4 py-3">Disposition</th><th className="px-4 py-3">Status</th><th className="px-4 py-3">Priority</th><th className="px-4 py-3">Created</th><th /></tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {items.map((item) => (
                    <tr key={item.id} className="group transition hover:bg-slate-50">
                      <td className="px-4 py-3.5"><Link href={`/intake/${item.id}`} className="font-mono text-xs font-medium text-indigo-700 hover:text-indigo-900">{item.artifactId.slice(0, 12)}…</Link>{item.isStale && <span className="ml-2 rounded-full bg-red-50 px-2 py-0.5 text-[10px] font-semibold text-red-700">STALE</span>}</td>
                      <td className="px-4 py-3.5 text-slate-700">{item.classificationCode || 'Unclassified'}</td>
                      <td className="px-4 py-3.5"><span className="rounded-full bg-slate-100 px-2 py-1 text-[11px] font-medium text-slate-600">{labelize(item.disposition)}</span></td>
                      <td className="px-4 py-3.5"><StatusBadge value={item.status} /></td>
                      <td className="px-4 py-3.5"><PriorityBadge value={item.priority} /></td>
                      <td className="whitespace-nowrap px-4 py-3.5 text-xs text-slate-500">{formatAge(item.createdAt)}</td>
                      <td className="px-4 py-3.5 text-right"><Link href={`/intake/${item.id}`} className="text-xs font-medium text-indigo-600 opacity-0 transition group-hover:opacity-100">Open <span aria-hidden>→</span></Link></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="flex items-center justify-between border-t border-slate-100 px-4 py-3 text-xs text-slate-500">
            <span>Page {page} of {totalPages}</span>
            <div className="flex gap-2"><button type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)} className="rounded-md border border-slate-200 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-40">Previous</button><button type="button" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)} className="rounded-md border border-slate-200 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-40">Next</button></div>
          </div>
        </section>
      </div>
    </div>
  );
}

function labelize(value: string) { return value.replaceAll('_', ' ').toLowerCase().replace(/\b\w/g, (match) => match.toUpperCase()); }
function formatAge(value: string) { const date = new Date(value); if (Number.isNaN(date.valueOf())) return value; return date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); }
function StatusBadge({ value }: { value: string }) { const tone = value === 'COMPLETED' ? 'bg-emerald-50 text-emerald-700' : value === 'IN_REVIEW' ? 'bg-indigo-50 text-indigo-700' : 'bg-amber-50 text-amber-700'; return <span className={`rounded-full px-2 py-1 text-[11px] font-semibold ${tone}`}>{labelize(value)}</span>; }
function PriorityBadge({ value }: { value: string }) { const tone = value === 'URGENT' ? 'text-red-700' : value === 'HIGH' ? 'text-orange-700' : 'text-slate-500'; return <span className={`text-xs font-semibold ${tone}`}>{labelize(value)}</span>; }