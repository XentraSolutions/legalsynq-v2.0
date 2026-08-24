'use client';

import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useCallback, useEffect, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import {
  cancelManualIntake,
  getManualIntake,
  retryManualArtifact,
  type IntakeArtifact,
  type ManualIntakeSubmission,
} from '@/lib/intake-api';

const statusStyle: Record<string, string> = {
  COMPLETED: 'bg-emerald-50 text-emerald-700',
  PARTIAL: 'bg-amber-50 text-amber-700',
  FAILED: 'bg-red-50 text-red-700',
  PROCESSING: 'bg-blue-50 text-blue-700',
  SKIPPED: 'bg-gray-100 text-gray-600',
};

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '—';
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function ManualIntakeDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const [submission, setSubmission] = useState<ManualIntakeSubmission | null>(null);
  const [loading, setLoading] = useState(true);
  const [acting, setActing] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSubmission(await getManualIntake(params.id));
    } catch {
      router.replace('/lien/intake/manual');
    } finally {
      setLoading(false);
    }
  }, [params.id, router]);

  useEffect(() => {
    void load();
  }, [load]);

  async function retry(artifact: IntakeArtifact, file?: File) {
    if (!file) {
      setError(`Choose ${artifact.originalFileName} before retrying.`);
      return;
    }
    setActing(artifact.id);
    setError(null);
    try {
      await retryManualArtifact(params.id, artifact.id, file);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Artifact retry failed.');
    } finally {
      setActing(null);
    }
  }

  async function cancel() {
    if (!submission) return;
    setActing('cancel');
    setError(null);
    try {
      await cancelManualIntake(submission.id, submission.version);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Cancellation failed.');
    } finally {
      setActing(null);
    }
  }

  if (loading) return <div className="rounded-xl border border-gray-200 bg-white px-6 py-16 text-center text-sm text-gray-400">Loading submission…</div>;
  if (!submission) return null;

  return (
    <div className="space-y-6">
      <PageHeader
        title={submission.title || 'Manual submission'}
        breadcrumbs={[
          { label: 'Manual Intake', href: '/lien/intake/manual' },
          { label: 'Submission details' },
        ]}
        subtitle={`${submission.purpose} · submitted ${formatDate(submission.submittedAt)}`}
        badge={
          <span className={`rounded-full px-2 py-1 text-[11px] font-medium ${statusStyle[submission.status] ?? 'bg-gray-100 text-gray-600'}`}>
            {submission.status}
          </span>
        }
        actions={
          submission.status !== 'CANCELLED' && submission.status !== 'COMPLETED' ? (
            <button
              onClick={() => void cancel()}
              disabled={acting !== null}
              className="rounded-lg border border-red-200 bg-white px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
            >
              Cancel submission
            </button>
          ) : undefined
        }
      />

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <section className="rounded-xl border border-gray-200 bg-white p-5">
        <div className="grid gap-4 text-sm sm:grid-cols-3">
          <div><p className="text-xs text-gray-400">Purpose</p><p className="mt-1 font-medium text-gray-800">{submission.purpose}</p></div>
          <div><p className="text-xs text-gray-400">Profile</p><p className="mt-1 font-medium text-gray-800">{submission.processingProfileCode}</p></div>
          <div><p className="text-xs text-gray-400">External reference</p><p className="mt-1 font-medium text-gray-800">{submission.externalReference || '—'}</p></div>
        </div>
        {submission.notes && <p className="mt-5 border-t border-gray-100 pt-4 text-sm leading-6 text-gray-600">{submission.notes}</p>}
      </section>

      <section className="rounded-xl border border-gray-200 bg-white">
        <div className="border-b border-gray-100 px-5 py-4">
          <h2 className="text-sm font-semibold text-gray-900">Artifacts</h2>
          <p className="mt-0.5 text-xs text-gray-500">Each file has independent Documents Service status and retry history.</p>
        </div>
        <div className="divide-y divide-gray-100">
          {submission.artifacts.map((artifact) => (
            <div key={artifact.id} className="px-5 py-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-3">
                  <i className="ri-file-text-line mt-0.5 text-lg text-gray-400" />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-gray-900">{artifact.originalFileName}</p>
                    <p className="mt-1 text-xs text-gray-500">{formatBytes(artifact.sizeBytes)} · {artifact.declaredContentType} · attempt {artifact.attemptCount}</p>
                    {artifact.failureMessage && <p className="mt-2 text-xs text-red-600">{artifact.failureMessage}</p>}
                  </div>
                </div>
                <span className={`rounded-full px-2 py-1 text-[11px] font-medium ${statusStyle[artifact.processingStatus] ?? 'bg-gray-100 text-gray-600'}`}>
                  {artifact.processingStatus}
                </span>
              </div>
              {artifact.isRetryable && (
                <div className="mt-3 flex flex-wrap items-center gap-2 pl-8">
                  <input
                    id={`retry-${artifact.id}`}
                    type="file"
                    className="max-w-full text-xs text-gray-500 file:mr-2 file:rounded file:border-0 file:bg-gray-100 file:px-2 file:py-1 file:text-xs"
                  />
                  <button
                    disabled={acting !== null}
                    onClick={() => {
                      const input = document.getElementById(`retry-${artifact.id}`) as HTMLInputElement | null;
                      void retry(artifact, input?.files?.[0]);
                    }}
                    className="rounded-md border border-indigo-200 px-3 py-1.5 text-xs font-medium text-indigo-700 hover:bg-indigo-50 disabled:opacity-50"
                  >
                    {acting === artifact.id ? 'Retrying…' : 'Retry artifact'}
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}