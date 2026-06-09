'use client';

import { useState, useTransition } from 'react';
import { clearMyNetworkAccessCode, setMyNetworkAccessCode } from '@/app/(platform)/careconnect/my-network/actions';
import { ConfirmDialog } from '@/components/lien/modal';
import type { CareConnectAccessCodeMetadata } from '@/lib/tenant-api';

interface Props {
  initialStatus: CareConnectAccessCodeMetadata;
}

export function PublicNetworkAccessCodePanel({ initialStatus }: Props) {
  const [status, setStatus] = useState(initialStatus);
  const [code, setCode] = useState('');
  const [revealedCode, setRevealedCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const [isPending, startTransition] = useTransition();

  function handleSave() {
    setError(null);
    setRevealedCode(null);
    startTransition(async () => {
      const result = await setMyNetworkAccessCode(code);
      if (!result.success) {
        setError(result.error ?? 'Failed to save access code.');
        return;
      }

      setStatus({
        configured: Boolean(result.configured),
        version: Number(result.version ?? 0),
        updatedAtUtc: result.updatedAtUtc ?? null,
      });
      setRevealedCode(result.revealedCode ?? null);
      setCode('');
    });
  }

  function handleClear() {
    setError(null);
    setRevealedCode(null);
    startTransition(async () => {
      const result = await clearMyNetworkAccessCode();
      if (!result.success) {
        setError(result.error ?? 'Failed to clear access code.');
        return;
      }

      setStatus({
        configured: Boolean(result.configured),
        version: Number(result.version ?? 0),
        updatedAtUtc: result.updatedAtUtc ?? null,
      });
      setCode('');
      setShowClearConfirm(false);
    });
  }

  return (
    <>
      <div className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-4 border-b border-gray-100 pb-5 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <h2 className="text-base font-semibold text-gray-900">Public Network Access Code</h2>
            <p className="max-w-2xl text-sm text-gray-500">
              This code protects the public provider directory for this tenant.
            </p>
          </div>
          <span
            className={[
              'inline-flex w-fit items-center rounded-full px-3 py-1 text-xs font-semibold',
              status.configured ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500',
            ].join(' ')}
          >
            {status.configured ? 'Configured' : 'Not configured'}
          </span>
        </div>

        <div className="mt-5 grid gap-4 xl:grid-cols-[minmax(0,1fr)_280px]">
          <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4">
            <label htmlFor="my-network-access-code" className="block text-sm font-medium text-gray-700">
              Access code
            </label>
            <input
              id="my-network-access-code"
              type="password"
              value={code}
              minLength={8}
              maxLength={128}
              onChange={e => { setCode(e.target.value); setError(null); setRevealedCode(null); }}
              placeholder={status.configured ? 'Replace current code' : 'Set access code'}
              className="mt-2 h-11 w-full rounded-xl border border-gray-200 bg-white px-3 text-sm text-gray-900 shadow-sm transition focus:border-transparent focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <p className="mt-2 text-xs text-gray-500">
              8 to 128 characters. Leading and trailing spaces are ignored.
            </p>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-gray-400">Details</p>
            <dl className="mt-3 space-y-3">
              <div className="flex items-start justify-between gap-4">
                <dt className="text-sm text-gray-500">State</dt>
                <dd className="text-sm font-medium text-gray-900">
                  {status.configured ? 'Configured' : 'Not configured'}
                </dd>
              </div>
              <div className="flex items-start justify-between gap-4">
                <dt className="text-sm text-gray-500">Version</dt>
                <dd className="text-sm font-medium text-gray-900">{status.version}</dd>
              </div>
              <div className="flex items-start justify-between gap-4">
                <dt className="text-sm text-gray-500">Last updated</dt>
                <dd className="max-w-[11rem] text-right text-sm font-medium text-gray-900">
                  {status.updatedAtUtc ? new Date(status.updatedAtUtc).toLocaleString() : 'Never'}
                </dd>
              </div>
            </dl>
          </div>
        </div>

        <div className="mt-4 flex flex-col gap-3 border-t border-gray-100 pt-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-sm text-amber-800">
            <p className="font-medium">The current code cannot be viewed later. Save it now.</p>
            <p className="mt-1 text-xs text-amber-700">
              Clearing the code keeps the directory locked until a new code is configured.
            </p>
          </div>

          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <button
              type="button"
              onClick={() => setShowClearConfirm(true)}
              disabled={isPending || !status.configured}
              className="inline-flex h-11 items-center justify-center rounded-xl border border-gray-200 px-4 text-sm font-semibold text-gray-600 transition hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Clear
            </button>
            <button
              type="button"
              onClick={handleSave}
              disabled={isPending || code.trim().length < 8}
              className="inline-flex h-11 items-center justify-center rounded-xl bg-blue-600 px-5 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {isPending ? 'Saving…' : status.configured ? 'Replace Code' : 'Set Code'}
            </button>
          </div>
        </div>

        {revealedCode && (
          <div className="mt-4 rounded-xl border border-green-100 bg-green-50 px-4 py-3">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-green-700">One-time reveal</p>
            <p className="mt-2 break-all font-mono text-sm text-green-900">{revealedCode}</p>
          </div>
        )}

        {error && (
          <p className="mt-4 rounded-xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </p>
        )}
      </div>

      <ConfirmDialog
        open={showClearConfirm}
        onClose={() => setShowClearConfirm(false)}
        onConfirm={handleClear}
        title="Clear access code?"
        description="The public directory will remain locked until a new access code is configured."
        confirmLabel="Clear code"
        confirmVariant="danger"
        loading={isPending}
      />
    </>
  );
}
