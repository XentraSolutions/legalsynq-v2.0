'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import {
  listManualIntake,
  submitManualIntake,
  type ManualIntakeSubmission,
} from '@/lib/intake-api';

const statusStyle: Record<string, string> = {
  COMPLETED: 'bg-emerald-50 text-emerald-700',
  PARTIAL: 'bg-amber-50 text-amber-700',
  FAILED: 'bg-red-50 text-red-700',
  PROCESSING: 'bg-blue-50 text-blue-700',
  CANCELLED: 'bg-gray-100 text-gray-600',
};

function formatDate(value?: string | null) {
  if (!value) return '—';
  return new Date(value).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function ManualIntakePage() {
  const [submissions, setSubmissions] = useState<ManualIntakeSubmission[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSubmissions((await listManualIntake()).items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load manual Intake history.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setSuccess(null);
    const form = new FormData(event.currentTarget);
    const files = form.getAll('files').filter((item): item is File => item instanceof File && item.size > 0);
    if (files.length === 0) {
      setError('Choose at least one file before submitting.');
      setSubmitting(false);
      return;
    }
    form.delete('files');
    for (const file of files) form.append('files', file);
    form.set('clientRequestId', crypto.randomUUID());

    try {
      const result = await submitManualIntake(form);
      setSuccess(
        `${result.artifacts.length} file${result.artifacts.length === 1 ? '' : 's'} submitted.`,
      );
      event.currentTarget.reset();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Manual Intake submission failed.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Manual Intake"
        subtitle="Submit source files directly to Intake. Files are validated, recorded with provenance, and sent through the Documents Service."
        actions={
          <Link
            href="/lien/intake/sources"
            className="rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50"
          >
            Manage sources
          </Link>
        }
      />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,420px)_1fr]">
        <section className="rounded-xl border border-gray-200 bg-white p-5">
          <div className="mb-5">
            <h2 className="text-sm font-semibold text-gray-900">New submission</h2>
            <p className="mt-1 text-xs leading-5 text-gray-500">
              Upload one or more bounded files. Unsupported file types are retained as failed artifacts for operator visibility.
            </p>
          </div>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Purpose</span>
              <select
                name="purpose"
                defaultValue="LIEN_INTAKE"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              >
                <option value="LIEN_INTAKE">Lien intake</option>
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Title (optional)</span>
              <input
                name="title"
                placeholder="e.g. Smith matter intake packet"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              />
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">External reference (optional)</span>
              <input
                name="externalReference"
                placeholder="Matter or operator reference"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              />
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Files</span>
              <input
                name="files"
                type="file"
                multiple
                required
                className="block w-full rounded-lg border border-dashed border-gray-300 bg-gray-50 px-3 py-4 text-sm text-gray-600 file:mr-3 file:rounded-md file:border-0 file:bg-indigo-600 file:px-3 file:py-1.5 file:text-xs file:font-medium file:text-white"
              />
              <span className="mt-1 block text-[11px] text-gray-400">Files are uploaded once; retry requires selecting the failed file again.</span>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-gray-600">Notes (optional)</span>
              <textarea
                name="notes"
                rows={3}
                placeholder="Context for the Intake operator"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              />
            </label>
            {error && <p className="rounded-lg bg-red-50 px-3 py-2 text-xs text-red-700">{error}</p>}
            {success && <p className="rounded-lg bg-emerald-50 px-3 py-2 text-xs text-emerald-700">{success}</p>}
            <button
              type="submit"
              disabled={submitting}
              className="w-full rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {submitting ? 'Submitting…' : 'Submit to Intake'}
            </button>
          </form>
        </section>

        <section className="min-w-0 rounded-xl border border-gray-200 bg-white">
          <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
            <div>
              <h2 className="text-sm font-semibold text-gray-900">Recent submissions</h2>
              <p className="mt-0.5 text-xs text-gray-500">Tenant-scoped manual Intake history</p>
            </div>
            <button onClick={() => void load()} className="text-xs font-medium text-indigo-600 hover:text-indigo-800">
              Refresh
            </button>
          </div>
          {loading ? (
            <div className="px-5 py-12 text-center text-sm text-gray-400">Loading history…</div>
          ) : submissions.length === 0 ? (
            <div className="px-5 py-12 text-center">
              <i className="ri-file-upload-line text-3xl text-gray-300" />
              <p className="mt-3 text-sm font-medium text-gray-600">No manual submissions yet</p>
              <p className="mt-1 text-xs text-gray-400">Submitted files will appear here with artifact-level results.</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-100">
              {submissions.map((submission) => {
                const size = submission.artifacts.reduce((total, item) => total + item.sizeBytes, 0);
                return (
                  <Link
                    key={submission.id}
                    href={`/lien/intake/manual/${submission.id}`}
                    className="block px-5 py-4 transition hover:bg-indigo-50/30"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-gray-900">
                          {submission.title || submission.externalReference || 'Untitled submission'}
                        </p>
                        <p className="mt-1 text-xs text-gray-500">
                          {submission.artifacts.length} file{submission.artifacts.length === 1 ? '' : 's'} · {formatBytes(size)} · {formatDate(submission.createdAt)}
                        </p>
                      </div>
                      <span className={`shrink-0 rounded-full px-2 py-1 text-[11px] font-medium ${statusStyle[submission.status] ?? 'bg-gray-100 text-gray-600'}`}>
                        {submission.status}
                      </span>
                    </div>
                  </Link>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}