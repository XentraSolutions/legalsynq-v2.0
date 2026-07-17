'use client';

import { useState, useEffect, useCallback, use } from 'react';
import { useRouter } from 'next/navigation';
import { PageHeader } from '@/components/lien/page-header';
import { useLienStore } from '@/stores/lien-store';
import type { EmailSource, ValidationHistoryEntry } from '@/lib/xenia-email-api';
import { DateDisplay } from '@/components/ui/date-display';

export const dynamic = 'force-dynamic';

const STATUS_COLORS: Record<string, string> = {
  Active:     'bg-green-100 text-green-800',
  Disabled:   'bg-gray-100 text-gray-600',
  Error:      'bg-red-100 text-red-800',
  Pending:    'bg-yellow-100 text-yellow-800',
  Validating: 'bg-blue-100 text-blue-800',
};

const HEALTH_COLORS: Record<string, string> = {
  Healthy:     'bg-green-100 text-green-800',
  Degraded:    'bg-yellow-100 text-yellow-800',
  Unavailable: 'bg-red-100 text-red-800',
  Unknown:     'bg-gray-100 text-gray-500',
};

const VALIDATION_COLORS: Record<string, string> = {
  Valid:          'bg-green-100 text-green-800',
  Invalid:        'bg-red-100 text-red-800',
  Pending:        'bg-blue-100 text-blue-800',
  NotValidated:   'bg-gray-100 text-gray-500',
};

interface EditState {
  displayName: string;
  description: string;
  username: string;
  incomingHost: string;
  incomingPort: string;
  useTls: boolean;
  mailboxFolder: string;
  secretReferenceId: string;
  oauthConnectionRef: string;
}

export default function EmailSourceDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router   = useRouter();
  const addToast = useLienStore((s) => s.addToast);

  const [source, setSource]   = useState<EmailSource | null>(null);
  const [history, setHistory] = useState<ValidationHistoryEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [acting, setActing]   = useState(false);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving]   = useState(false);
  const [edit, setEdit]       = useState<EditState | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [sRes, histRes] = await Promise.all([
        fetch(`/api/xenia/email/sources/${id}`),
        fetch(`/api/xenia/email/sources/${id}/validation-history?limit=8`).catch(() => null),
      ]);
      if (!sRes.ok) { router.replace('/lien/settings/email-sources'); return; }
      const s: EmailSource = await sRes.json();
      setSource(s);
      if (histRes?.ok) {
        const hd = await histRes.json();
        setHistory(hd.history ?? []);
      }
    } catch {
    } finally {
      setLoading(false);
    }
  }, [id, router]);

  useEffect(() => { load(); }, [load]);

  function startEdit(s: EmailSource) {
    setEdit({
      displayName:       s.displayName,
      description:       s.description ?? '',
      username:          s.username ?? '',
      incomingHost:      s.incomingHost ?? '',
      incomingPort:      s.incomingPort ? String(s.incomingPort) : '',
      useTls:            s.useTls,
      mailboxFolder:     s.mailboxFolder ?? '',
      secretReferenceId: '',
      oauthConnectionRef: '',
    });
    setEditing(true);
  }

  async function saveEdit() {
    if (!source || !edit) return;
    setSaving(true);
    try {
      const payload = {
        displayName:       edit.displayName,
        description:       edit.description || undefined,
        username:          edit.username || undefined,
        incomingHost:      edit.incomingHost || undefined,
        incomingPort:      edit.incomingPort ? Number(edit.incomingPort) : undefined,
        useTls:            edit.useTls,
        mailboxFolder:     edit.mailboxFolder || undefined,
        secretReferenceId: edit.secretReferenceId || undefined,
        oauthConnectionRef: edit.oauthConnectionRef || undefined,
        expectedRowVersion: source.rowVersion,
      };
      const res = await fetch(`/api/xenia/email/sources/${id}`, {
        method:  'PUT',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(payload),
      });
      if (!res.ok) {
        const d = await res.json().catch(() => ({}));
        throw new Error(d.error ?? `HTTP ${res.status}`);
      }
      addToast({ type: 'success', title: 'Source updated' });
      setEditing(false);
      await load();
    } catch (err) {
      addToast({ type: 'error', title: 'Update failed', description: err instanceof Error ? err.message : '' });
    } finally {
      setSaving(false);
    }
  }

  async function handleValidate() {
    setActing(true);
    try {
      const res = await fetch(`/api/xenia/email/sources/${id}/validate`, { method: 'POST' });
      const d   = await res.json();
      if (d.success) {
        addToast({ type: 'success', title: 'Connection validated', description: `${d.durationMs} ms` });
      } else {
        addToast({ type: 'error', title: 'Validation failed', description: d.safeErrorSummary ?? d.errorCode ?? '' });
      }
      await load();
    } catch (err) {
      addToast({ type: 'error', title: 'Validation error', description: err instanceof Error ? err.message : '' });
    } finally {
      setActing(false);
    }
  }

  async function toggleEnabled() {
    if (!source) return;
    setActing(true);
    try {
      const action = source.enabled ? 'disable' : 'enable';
      const res = await fetch(`/api/xenia/email/sources/${id}/${action}`, { method: 'PUT' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      addToast({ type: 'success', title: source.enabled ? 'Source disabled' : 'Source enabled' });
      await load();
    } catch (err) {
      addToast({ type: 'error', title: 'Action failed', description: err instanceof Error ? err.message : '' });
    } finally {
      setActing(false);
    }
  }

  async function handleDelete() {
    if (!source || !confirm(`Delete "${source.displayName}"? This cannot be undone.`)) return;
    setActing(true);
    try {
      const res = await fetch(`/api/xenia/email/sources/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      addToast({ type: 'success', title: 'Source deleted' });
      router.push('/lien/settings/email-sources');
    } catch (err) {
      addToast({ type: 'error', title: 'Delete failed', description: err instanceof Error ? err.message : '' });
      setActing(false);
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-sm text-gray-400">Loading…</div>
    );
  }

  if (!source) return null;

  return (
    <div className="space-y-6">
      <PageHeader
        title={source.displayName}
        subtitle={source.emailAddress}
        breadcrumbs={[
          { label: 'Email Sources', href: '/lien/settings/email-sources' },
          { label: source.displayName },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[source.status] ?? 'bg-gray-100 text-gray-600'}`}>
              {source.status}
            </span>
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${HEALTH_COLORS[source.healthStatus] ?? 'bg-gray-100 text-gray-500'}`}>
              {source.healthStatus}
            </span>
            <button
              onClick={handleValidate}
              disabled={acting}
              className="inline-flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
            >
              <i className="ri-wifi-line" /> Test Connection
            </button>
            <button
              onClick={toggleEnabled}
              disabled={acting}
              className="inline-flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
            >
              {source.enabled ? <><i className="ri-pause-line" /> Disable</> : <><i className="ri-play-line" /> Enable</>}
            </button>
            <button
              onClick={handleDelete}
              disabled={acting}
              className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
            >
              <i className="ri-delete-bin-line" /> Delete
            </button>
          </div>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
              <p className="text-sm font-semibold text-gray-700">Configuration</p>
              {!editing && (
                <button
                  onClick={() => startEdit(source)}
                  className="text-xs font-medium text-indigo-600 hover:text-indigo-800"
                >
                  Edit
                </button>
              )}
            </div>

            {editing && edit ? (
              <div className="px-4 py-4 space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Display Name</label>
                    <input
                      type="text" maxLength={200} value={edit.displayName}
                      onChange={(e) => setEdit({ ...edit, displayName: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Username</label>
                    <input
                      type="text" maxLength={320} value={edit.username}
                      onChange={(e) => setEdit({ ...edit, username: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Incoming Host</label>
                    <input
                      type="text" maxLength={253} value={edit.incomingHost} placeholder="mail.example.com"
                      onChange={(e) => setEdit({ ...edit, incomingHost: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Port</label>
                    <input
                      type="number" min={1} max={65535} value={edit.incomingPort} placeholder="993"
                      onChange={(e) => setEdit({ ...edit, incomingPort: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Mailbox Folder</label>
                    <input
                      type="text" maxLength={200} value={edit.mailboxFolder} placeholder="INBOX"
                      onChange={(e) => setEdit({ ...edit, mailboxFolder: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div className="flex items-center gap-2 pt-5">
                    <input
                      id="edit-useTls" type="checkbox" checked={edit.useTls}
                      onChange={(e) => setEdit({ ...edit, useTls: e.target.checked })}
                      className="h-4 w-4 rounded border-gray-300 text-indigo-600"
                    />
                    <label htmlFor="edit-useTls" className="text-sm font-medium text-gray-700">Use TLS</label>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Description</label>
                    <input
                      type="text" maxLength={500} value={edit.description}
                      onChange={(e) => setEdit({ ...edit, description: e.target.value })}
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">New Secret Reference ID</label>
                    <input
                      type="text" maxLength={500} value={edit.secretReferenceId}
                      onChange={(e) => setEdit({ ...edit, secretReferenceId: e.target.value })}
                      placeholder="Leave blank to keep existing"
                      className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                    />
                  </div>
                </div>
                <div className="border-t border-gray-100 pt-3 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setEditing(false)}
                    className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    onClick={saveEdit}
                    disabled={saving}
                    className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
                  >
                    {saving ? 'Saving…' : 'Save Changes'}
                  </button>
                </div>
              </div>
            ) : (
              <dl className="divide-y divide-gray-100 px-4 py-2">
                {[
                  ['Provider',        source.providerType],
                  ['Auth Type',       source.authType],
                  ['Email Address',   source.emailAddress],
                  ['Username',        source.username ?? '—'],
                  ['Incoming Host',   source.incomingHost ? `${source.incomingHost}:${source.incomingPort ?? '—'}` : '—'],
                  ['TLS',             source.useTls ? 'Yes' : 'No'],
                  ['Mailbox Folder',  source.mailboxFolder ?? 'INBOX'],
                  ['Credentials',     source.hasOAuthConnection ? 'OAuth connection' : source.hasSecretReference ? 'Secret reference' : 'None configured'],
                ].map(([label, value]) => (
                  <div key={label} className="flex py-2.5 gap-4">
                    <dt className="w-36 flex-shrink-0 text-xs font-medium text-gray-500">{label}</dt>
                    <dd className="text-sm text-gray-900 font-mono">{value}</dd>
                  </div>
                ))}
              </dl>
            )}
          </div>

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">Validation History</p>
            </div>
            {history.length === 0 ? (
              <p className="px-4 py-6 text-center text-sm text-gray-400">No validation history yet. Run a connection test above.</p>
            ) : (
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-gray-100 text-gray-500 uppercase tracking-wide">
                    <th className="px-4 py-2 text-left">Result</th>
                    <th className="px-4 py-2 text-left">Duration</th>
                    <th className="px-4 py-2 text-left">Started</th>
                    <th className="px-4 py-2 text-left">Error</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {history.map((h) => (
                    <tr key={h.id} className="hover:bg-gray-50/40">
                      <td className="px-4 py-2">
                        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${VALIDATION_COLORS[h.result] ?? 'bg-gray-100 text-gray-500'}`}>
                          {h.result}
                        </span>
                      </td>
                      <td className="px-4 py-2 font-mono text-gray-600">
                        {h.durationMs != null ? `${h.durationMs} ms` : '—'}
                      </td>
                      <td className="px-4 py-2 text-gray-500">
                        <DateDisplay value={h.startedAt} format="datetime" />
                      </td>
                      <td className="px-4 py-2 text-red-500 font-mono">{h.errorSummary ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">Status</p>
            </div>
            <dl className="divide-y divide-gray-100 px-4 py-2">
              {[
                ['Validation', <span key="v" className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${VALIDATION_COLORS[source.validationStatus] ?? 'bg-gray-100 text-gray-500'}`}>{source.validationStatus}</span>],
                ['Last validated', source.lastValidatedAt ? <DateDisplay key="lv" value={source.lastValidatedAt} format="datetime" /> : '—'],
                ['Last connected', source.lastConnectionAt ? <DateDisplay key="lc" value={source.lastConnectionAt} format="datetime" /> : '—'],
                ['Latency', source.lastValidationLatencyMs != null ? `${source.lastValidationLatencyMs} ms` : '—'],
                ['Enabled', source.enabled ? 'Yes' : 'No'],
              ].map(([label, value]) => (
                <div key={String(label)} className="flex py-2.5 gap-3">
                  <dt className="w-28 flex-shrink-0 text-xs font-medium text-gray-500">{label}</dt>
                  <dd className="text-xs text-gray-900">{value}</dd>
                </div>
              ))}
            </dl>
          </div>

          {source.lastErrorSummary && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3">
              <p className="text-xs font-semibold text-red-700 mb-1">Last Error</p>
              <p className="text-xs text-red-600 font-mono">{source.lastErrorCode}</p>
              <p className="text-xs text-red-500 mt-1">{source.lastErrorSummary}</p>
            </div>
          )}

          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <p className="text-sm font-semibold text-gray-700">Record</p>
            </div>
            <dl className="divide-y divide-gray-100 px-4 py-2">
              {[
                ['Created', <DateDisplay key="c" value={source.createdAtUtc} format="date" />],
                ['Updated', <DateDisplay key="u" value={source.updatedAtUtc} format="date" />],
                ['Version', String(source.rowVersion)],
              ].map(([label, value]) => (
                <div key={String(label)} className="flex py-2.5 gap-3">
                  <dt className="w-20 flex-shrink-0 text-xs font-medium text-gray-500">{label}</dt>
                  <dd className="text-xs text-gray-700">{value}</dd>
                </div>
              ))}
            </dl>
          </div>
        </div>
      </div>
    </div>
  );
}
