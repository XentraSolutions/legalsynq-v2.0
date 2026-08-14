'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import {
  createIntakeSource,
  listIntakePurposes,
  listIntakeSourceTypes,
  listIntakeSources,
  setIntakeSourceStatus,
  validateIntakeSource,
  type IntakeSource,
  type IntakeSourceCode,
} from '@/lib/intake-api';

export default function IntakeSourcesPage() {
  const [sources, setSources] = useState<IntakeSource[]>([]);
  const [types, setTypes] = useState<IntakeSourceCode[]>([]);
  const [purposes, setPurposes] = useState<IntakeSourceCode[]>([]);
  const [loading, setLoading] = useState(true);
  const [acting, setActing] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [sourceResult, typeResult, purposeResult] = await Promise.all([
        listIntakeSources(),
        listIntakeSourceTypes(),
        listIntakePurposes(),
      ]);
      setSources(sourceResult);
      setTypes(typeResult);
      setPurposes(purposeResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load Intake sources.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function createSource(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setActing('create');
    setError(null);
    setMessage(null);
    const form = new FormData(event.currentTarget);
    try {
      await createIntakeSource({
        sourceType: form.get('sourceType'),
        emailAddress: form.get('emailAddress'),
        provider: 'GENERIC',
        purpose: form.get('purpose'),
        processingProfileCode: form.get('processingProfileCode'),
        connectorConfiguration: {},
        isDefault: false,
      });
      event.currentTarget.reset();
      setMessage('Source registered.');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to register source.');
    } finally {
      setActing(null);
    }
  }

  async function validate(source: IntakeSource) {
    setActing(source.sourceId);
    setError(null);
    try {
      await validateIntakeSource(source.sourceId, source.configurationVersion);
      setMessage('Source configuration validated. Live mailbox connectivity is not asserted.');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Source validation failed.');
    } finally {
      setActing(null);
    }
  }

  async function toggle(source: IntakeSource) {
    setActing(source.sourceId);
    setError(null);
    try {
      await setIntakeSourceStatus(source.sourceId, !source.isActive, source.configurationVersion);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Source status update failed.');
    } finally {
      setActing(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Intake Sources"
        breadcrumbs={[{ label: 'Manual Intake', href: '/lien/intake/manual' }, { label: 'Sources' }]}
        subtitle="Manage tenant-scoped Intake source configuration. Manual submissions use explicit MANUAL provenance and do not require a mailbox."
      />
      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
      {message && <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">{message}</div>}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,360px)_1fr]">
        <section className="rounded-xl border border-gray-200 bg-white p-5">
          <h2 className="text-sm font-semibold text-gray-900">Register a source</h2>
          <p className="mt-1 text-xs leading-5 text-gray-500">
            Email sources remain configuration-only in this release. Manual intake is available from the submission page.
          </p>
          <form onSubmit={createSource} className="mt-5 space-y-4">
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Source type</span>
              <select name="sourceType" defaultValue={types[0]?.code ?? 'EMAIL'} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
                {types.filter((item) => item.code === 'EMAIL').map((item) => <option key={item.code} value={item.code}>{item.displayName}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Email address</span>
              <input name="emailAddress" type="email" required placeholder="intake@example.com" className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Purpose</span>
              <select name="purpose" defaultValue={purposes[0]?.code ?? 'LIEN_INTAKE'} className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
                {purposes.map((item) => <option key={item.code} value={item.code}>{item.displayName}</option>)}
              </select>
            </label>
            <input type="hidden" name="processingProfileCode" value="LIEN_INTAKE_V1" />
            <button type="submit" disabled={acting === 'create'} className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {acting === 'create' ? 'Registering…' : 'Register email source'}
            </button>
          </form>
        </section>

        <section className="rounded-xl border border-gray-200 bg-white">
          <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
            <div><h2 className="text-sm font-semibold text-gray-900">Configured sources</h2><p className="mt-0.5 text-xs text-gray-500">B03 source management with tenant isolation and optimistic versions</p></div>
            <Link href="/lien/intake/manual" className="text-xs font-medium text-indigo-600 hover:text-indigo-800">Manual intake →</Link>
          </div>
          {loading ? <div className="px-5 py-12 text-center text-sm text-gray-400">Loading sources…</div> : sources.length === 0 ? <div className="px-5 py-12 text-center text-sm text-gray-400">No sources configured.</div> : (
            <div className="divide-y divide-gray-100">
              {sources.map((source) => (
                <div key={source.sourceId} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                  <div><p className="text-sm font-medium text-gray-900">{source.emailAddress}</p><p className="mt-1 text-xs text-gray-500">{source.sourceType} · {source.purpose} · v{source.configurationVersion}</p></div>
                  <div className="flex items-center gap-2">
                    <span className={`rounded-full px-2 py-1 text-[11px] font-medium ${source.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-gray-100 text-gray-600'}`}>{source.isActive ? 'ACTIVE' : 'INACTIVE'}</span>
                    <button onClick={() => void validate(source)} disabled={acting === source.sourceId} className="rounded-md px-2 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-50 disabled:opacity-50">Validate</button>
                    <button onClick={() => void toggle(source)} disabled={acting === source.sourceId} className="rounded-md px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100 disabled:opacity-50">{source.isActive ? 'Disable' : 'Enable'}</button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}