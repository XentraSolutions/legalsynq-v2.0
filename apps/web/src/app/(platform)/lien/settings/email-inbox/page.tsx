'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import type { EmailMessageSummary, EmailSource } from '@/lib/xenia-email-api';

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 50;

const IMPORT_COLORS: Record<string, string> = {
  Imported:  'bg-green-100 text-green-800',
  Duplicate: 'bg-gray-100 text-gray-500',
  Updated:   'bg-blue-100 text-blue-800',
  Failed:    'bg-red-100 text-red-800',
  Pending:   'bg-yellow-100 text-yellow-800',
};

const IMPORTANCE_ICON: Record<string, string> = {
  High:   '🔴',
  Low:    '⬇️',
  Normal: '',
};

function formatDate(iso?: string) {
  if (!iso) return '—';
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffDays = diffMs / 86400000;
  if (diffDays < 1) return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
  if (diffDays < 7) return d.toLocaleDateString('en-US', { weekday: 'short', hour: '2-digit', minute: '2-digit' });
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export default function EmailInboxPage() {
  const [messages, setMessages]     = useState<EmailMessageSummary[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [sources, setSources]       = useState<EmailSource[]>([]);
  const [loading, setLoading]       = useState(true);
  const [error, setError]           = useState<string | null>(null);
  const [pageOffset, setPageOffset] = useState(0);

  const [filterSource,  setFilterSource]  = useState('');
  const [filterFrom,    setFilterFrom]    = useState('');
  const [filterSubject, setFilterSubject] = useState('');
  const [filterStatus,  setFilterStatus]  = useState('');

  const subjectRef = useRef<HTMLInputElement>(null);
  const fromRef    = useRef<HTMLInputElement>(null);

  const load = useCallback(async (offset: number, src: string, from: string, subj: string, status: string) => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (src)    params.set('sourceId',     src);
      if (from)   params.set('fromAddress',  from);
      if (subj)   params.set('subject',      subj);
      if (status) params.set('importStatus', status);
      params.set('pageSize',   String(PAGE_SIZE));
      params.set('pageOffset', String(offset));

      const res = await fetch(`/api/xenia/email/messages?${params}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setMessages(data.messages ?? []);
      setTotalCount(data.totalCount ?? 0);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load messages');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetch('/api/xenia/email/sources')
      .then((r) => r.json())
      .then((d) => setSources(d.sources ?? []))
      .catch(() => {});
  }, []);

  useEffect(() => {
    load(pageOffset, filterSource, filterFrom, filterSubject, filterStatus);
  }, [load, pageOffset, filterSource, filterFrom, filterSubject, filterStatus]);

  function applyTextFilters() {
    setPageOffset(0);
    setFilterFrom(fromRef.current?.value ?? '');
    setFilterSubject(subjectRef.current?.value ?? '');
  }

  function clearFilters() {
    setFilterSource('');
    setFilterFrom('');
    setFilterSubject('');
    setFilterStatus('');
    setPageOffset(0);
    if (fromRef.current)    fromRef.current.value    = '';
    if (subjectRef.current) subjectRef.current.value = '';
  }

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);
  const currentPage = Math.floor(pageOffset / PAGE_SIZE) + 1;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Email Inbox"
        subtitle={totalCount > 0 ? `${totalCount.toLocaleString()} messages pulled from connected mailboxes` : 'Messages pulled from your connected email sources appear here'}
        actions={
          <Link
            href="/lien/settings/email-sources"
            className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-50"
          >
            <i className="ri-mail-settings-line" /> Manage Sources
          </Link>
        }
      />

      {/* Filters */}
      <div className="rounded-lg border border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Source</label>
            <select
              value={filterSource}
              onChange={(e) => { setFilterSource(e.target.value); setPageOffset(0); }}
              className="rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            >
              <option value="">All sources</option>
              {sources.map((s) => (
                <option key={s.id} value={s.id}>{s.displayName}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Status</label>
            <select
              value={filterStatus}
              onChange={(e) => { setFilterStatus(e.target.value); setPageOffset(0); }}
              className="rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            >
              <option value="">All statuses</option>
              {['Imported','Duplicate','Updated','Failed','Pending'].map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">From</label>
            <input
              ref={fromRef}
              type="text"
              defaultValue={filterFrom}
              placeholder="sender@example.com"
              onKeyDown={(e) => e.key === 'Enter' && applyTextFilters()}
              className="rounded-md border border-gray-300 px-2 py-1.5 text-sm w-48 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Subject</label>
            <input
              ref={subjectRef}
              type="text"
              defaultValue={filterSubject}
              placeholder="Search subject…"
              onKeyDown={(e) => e.key === 'Enter' && applyTextFilters()}
              className="rounded-md border border-gray-300 px-2 py-1.5 text-sm w-48 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>
          <button
            type="button"
            onClick={applyTextFilters}
            className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
          >
            Search
          </button>
          {(filterSource || filterFrom || filterSubject || filterStatus) && (
            <button
              type="button"
              onClick={clearFilters}
              className="text-xs text-gray-400 hover:text-gray-600"
            >
              Clear filters
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="rounded-lg border border-gray-200 bg-white px-6 py-16 text-center text-sm text-gray-400">
          Loading messages…
        </div>
      ) : messages.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 bg-white px-6 py-16 text-center">
          <i className="ri-inbox-line text-4xl text-gray-300" />
          <p className="mt-3 text-sm font-medium text-gray-600">
            {(filterSource || filterFrom || filterSubject || filterStatus) ? 'No messages match your filters' : 'No messages yet'}
          </p>
          <p className="mt-1 text-xs text-gray-400">
            {(filterSource || filterFrom || filterSubject || filterStatus)
              ? 'Try broadening your search.'
              : 'Once Xenia pulls email from a connected source, messages will appear here.'}
          </p>
          {!filterSource && !filterFrom && !filterSubject && !filterStatus && (
            <Link
              href="/lien/settings/email-sources"
              className="mt-4 inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800"
            >
              <i className="ri-mail-settings-line" /> Set up an email source
            </Link>
          )}
        </div>
      ) : (
        <>
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50 text-xs font-medium text-gray-500 uppercase tracking-wide">
                  <th className="px-4 py-3 text-left w-5" />
                  <th className="px-4 py-3 text-left">From</th>
                  <th className="px-4 py-3 text-left">Subject</th>
                  <th className="px-4 py-3 text-left">Source</th>
                  <th className="px-4 py-3 text-left">Status</th>
                  <th className="px-4 py-3 text-right">Received</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {messages.map((m) => {
                  const src = sources.find((s) => s.id === m.emailSourceId);
                  return (
                    <tr key={m.id} className="hover:bg-indigo-50/30 cursor-pointer group">
                      <td className="px-4 py-3 text-center text-xs">
                        {IMPORTANCE_ICON[m.importance] || ''}
                        {m.hasAttachments && (
                          <i className="ri-attachment-2 text-gray-400 ml-0.5" />
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <p className="font-medium text-gray-900 truncate max-w-[180px]">
                          {m.fromName || m.fromAddress || '—'}
                        </p>
                        {m.fromName && (
                          <p className="text-xs text-gray-400 truncate max-w-[180px] font-mono">{m.fromAddress}</p>
                        )}
                      </td>
                      <td className="px-4 py-3 max-w-xs">
                        <Link
                          href={`/lien/settings/email-inbox/${m.id}`}
                          className="block"
                        >
                          <p className="font-medium text-gray-900 group-hover:text-indigo-700 truncate">
                            {m.subject || <span className="italic text-gray-400">(no subject)</span>}
                          </p>
                          {m.bodyPreview && (
                            <p className="text-xs text-gray-400 truncate mt-0.5">{m.bodyPreview}</p>
                          )}
                        </Link>
                      </td>
                      <td className="px-4 py-3 text-xs text-gray-500 truncate max-w-[120px]">
                        {src?.displayName ?? <span className="font-mono text-gray-300">{m.emailSourceId.slice(0, 8)}…</span>}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${IMPORT_COLORS[m.importStatus] ?? 'bg-gray-100 text-gray-500'}`}>
                          {m.importStatus}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-gray-400 text-right whitespace-nowrap">
                        {formatDate(m.receivedAt ?? m.importedAt)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-xs text-gray-500">
              <span>
                Page {currentPage} of {totalPages} · {totalCount.toLocaleString()} messages
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPageOffset(Math.max(0, pageOffset - PAGE_SIZE))}
                  disabled={pageOffset === 0}
                  className="rounded border border-gray-200 px-3 py-1.5 font-medium hover:bg-gray-50 disabled:opacity-40"
                >
                  ← Previous
                </button>
                <button
                  onClick={() => setPageOffset(pageOffset + PAGE_SIZE)}
                  disabled={pageOffset + PAGE_SIZE >= totalCount}
                  className="rounded border border-gray-200 px-3 py-1.5 font-medium hover:bg-gray-50 disabled:opacity-40"
                >
                  Next →
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
