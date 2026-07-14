'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { useLienStore } from '@/stores/lien-store';
import type { EmailSource } from '@/lib/xenia-email-api';

const STATUS_COLORS: Record<string, string> = {
  Active:     'bg-green-100 text-green-800',
  Disabled:   'bg-gray-100 text-gray-600',
  Error:      'bg-red-100 text-red-800',
  Pending:    'bg-yellow-100 text-yellow-800',
  Validating: 'bg-blue-100 text-blue-800',
};

const HEALTH_COLORS: Record<string, string> = {
  Healthy:     'text-green-600',
  Degraded:    'text-yellow-600',
  Unavailable: 'text-red-600',
  Unknown:     'text-gray-400',
};

export const dynamic = 'force-dynamic';

export default function EmailSourcesPage() {
  const addToast = useLienStore((s) => s.addToast);
  const [sources, setSources]   = useState<EmailSource[]>([]);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState<string | null>(null);
  const [acting, setActing]     = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/xenia/email/sources');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setSources(data.sources ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load email sources');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  async function toggleEnabled(source: EmailSource) {
    setActing(source.id);
    try {
      const action = source.enabled ? 'disable' : 'enable';
      const res = await fetch(`/api/xenia/email/sources/${source.id}/${action}`, { method: 'PUT' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      addToast({ type: 'success', title: source.enabled ? 'Source disabled' : 'Source enabled' });
      await load();
    } catch (e) {
      addToast({ type: 'error', title: 'Action failed', description: e instanceof Error ? e.message : '' });
    } finally {
      setActing(null);
    }
  }

  async function handleDelete(source: EmailSource) {
    if (!confirm(`Delete "${source.displayName}"? This cannot be undone.`)) return;
    setActing(source.id);
    try {
      const res = await fetch(`/api/xenia/email/sources/${source.id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      addToast({ type: 'success', title: 'Source deleted' });
      await load();
    } catch (e) {
      addToast({ type: 'error', title: 'Delete failed', description: e instanceof Error ? e.message : '' });
    } finally {
      setActing(null);
    }
  }

  async function handleValidate(source: EmailSource) {
    setActing(source.id);
    try {
      const res = await fetch(`/api/xenia/email/sources/${source.id}/validate`, { method: 'POST' });
      const data = await res.json();
      if (data.success) {
        addToast({ type: 'success', title: 'Connection validated', description: `${data.durationMs} ms` });
      } else {
        addToast({ type: 'error', title: 'Validation failed', description: data.safeErrorSummary ?? data.errorCode ?? '' });
      }
      await load();
    } catch (e) {
      addToast({ type: 'error', title: 'Validation failed', description: e instanceof Error ? e.message : '' });
    } finally {
      setActing(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Email Sources"
        subtitle="Connect mailboxes so Xenia can pull incoming email into the platform."
        actions={
          <Link
            href="/lien/settings/email-sources/new"
            className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <i className="ri-add-line" /> Add Email Source
          </Link>
        }
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="rounded-lg border border-gray-200 bg-white px-6 py-10 text-center text-sm text-gray-400">
          Loading…
        </div>
      ) : sources.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 bg-white px-6 py-12 text-center">
          <i className="ri-mail-settings-line text-3xl text-gray-300" />
          <p className="mt-3 text-sm font-medium text-gray-600">No email sources configured</p>
          <p className="mt-1 text-xs text-gray-400">
            Add an email source to let Xenia pull messages from your mailbox.
          </p>
          <Link
            href="/lien/settings/email-sources/new"
            className="mt-4 inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            <i className="ri-add-line" /> Add Email Source
          </Link>
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50 text-xs font-medium text-gray-500 uppercase tracking-wide">
                <th className="px-4 py-3 text-left">Source</th>
                <th className="px-4 py-3 text-left">Provider</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Health</th>
                <th className="px-4 py-3 text-left">Last connected</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {sources.map((s) => (
                <tr key={s.id} className="hover:bg-gray-50/50">
                  <td className="px-4 py-3">
                    <Link
                      href={`/lien/settings/email-sources/${s.id}`}
                      className="font-medium text-gray-900 hover:text-indigo-600"
                    >
                      {s.displayName}
                    </Link>
                    <div className="text-xs text-gray-400 font-mono mt-0.5">{s.emailAddress}</div>
                  </td>
                  <td className="px-4 py-3 text-gray-600">
                    {s.providerType}
                    <span className="ml-1.5 text-xs text-gray-400">({s.authType})</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[s.status] ?? 'bg-gray-100 text-gray-600'}`}>
                      {s.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`text-xs font-medium ${HEALTH_COLORS[s.healthStatus] ?? 'text-gray-400'}`}>
                      {s.healthStatus}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-400">
                    {s.lastConnectionAt
                      ? new Date(s.lastConnectionAt).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
                      : '—'}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => handleValidate(s)}
                        disabled={acting === s.id}
                        className="rounded px-2 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-50 disabled:opacity-40"
                      >
                        Test
                      </button>
                      <button
                        onClick={() => toggleEnabled(s)}
                        disabled={acting === s.id}
                        className="rounded px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100 disabled:opacity-40"
                      >
                        {s.enabled ? 'Disable' : 'Enable'}
                      </button>
                      <Link
                        href={`/lien/settings/email-sources/${s.id}`}
                        className="rounded px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100"
                      >
                        Edit
                      </Link>
                      <button
                        onClick={() => handleDelete(s)}
                        disabled={acting === s.id}
                        className="rounded px-2 py-1 text-xs font-medium text-red-500 hover:bg-red-50 disabled:opacity-40"
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
