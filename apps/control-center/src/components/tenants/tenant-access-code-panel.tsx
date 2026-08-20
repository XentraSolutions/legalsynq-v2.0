'use client';

import { useState, useTransition } from 'react';
import { clearTenantAccessCode, setTenantAccessCode } from '@/app/tenants/[id]/actions';
import type { TenantAccessCodeMetadata } from '@/types/control-center';

interface Props {
  tenantId: string;
  initialStatus: TenantAccessCodeMetadata;
}

export function TenantAccessCodePanel({ tenantId, initialStatus }: Props) {
  const [status, setStatus] = useState(initialStatus);
  const [code, setCode] = useState('');
  const [revealedCode, setRevealedCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function handleSave() {
    setError(null);
    setRevealedCode(null);
    startTransition(async () => {
      const result = await setTenantAccessCode(tenantId, code);
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
    if (!confirm('Clear the saved access code? The directory will stay locked until a new code is configured.')) {
      return;
    }

    setError(null);
    setRevealedCode(null);
    startTransition(async () => {
      const result = await clearTenantAccessCode(tenantId);
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
    });
  }

  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-lg bg-amber-50 flex items-center justify-center">
            <i className="ri-lock-password-line text-amber-600 text-sm" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-gray-900">Public Network Access Code</h3>
            <p className="text-[11px] text-gray-400 mt-0.5">
              This code protects the public provider directory for this tenant.
            </p>
          </div>
        </div>
        <span
          className={[
            'text-[10px] font-semibold px-2 py-0.5 rounded',
            status.configured ? 'text-green-700 bg-green-100' : 'text-gray-500 bg-gray-100',
          ].join(' ')}
        >
          {status.configured ? 'CONFIGURED' : 'NOT CONFIGURED'}
        </span>
      </div>

      <div className="px-5 py-5 space-y-4">
        <div>
          <label htmlFor="tenant-access-code" className="block text-xs font-medium text-gray-600 mb-1.5">
            Access code
          </label>
          <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
            <input
              id="tenant-access-code"
              type="password"
              value={code}
              minLength={8}
              maxLength={128}
              onChange={e => { setCode(e.target.value); setError(null); setRevealedCode(null); }}
              placeholder={status.configured ? 'Replace current code' : 'Set access code'}
              className="h-[38px] w-full rounded-lg border border-gray-200 px-3 text-sm focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent"
            />

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleSave}
                disabled={isPending || code.trim().length < 8}
                className="h-[38px] px-4 rounded-lg bg-amber-600 text-white text-xs font-semibold hover:bg-amber-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                {isPending ? 'Saving…' : status.configured ? 'Replace Code' : 'Set Code'}
              </button>
              <button
                type="button"
                onClick={handleClear}
                disabled={isPending || !status.configured}
                className="h-[38px] px-4 rounded-lg border border-gray-200 text-xs font-semibold text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                Clear
              </button>
            </div>
          </div>
          <p className="mt-1 text-xs text-gray-400">8 to 128 characters. Leading and trailing spaces are ignored.</p>
        </div>

        <div className="grid gap-2 text-xs text-gray-500 sm:grid-cols-3">
          <div>
            <span className="font-medium text-gray-700">State:</span> {status.configured ? 'Configured' : 'Not configured'}
          </div>
          <div>
            <span className="font-medium text-gray-700">Version:</span> {status.version}
          </div>
          <div>
            <span className="font-medium text-gray-700">Last updated:</span>{' '}
            {status.updatedAtUtc ? new Date(status.updatedAtUtc).toLocaleString() : 'Never'}
          </div>
        </div>

        <div className="rounded-lg border border-amber-100 bg-amber-50 px-3.5 py-3 text-xs text-amber-800">
          <p>The current code cannot be viewed later. Save it now.</p>
          <p className="mt-1">Clearing the code keeps the directory locked until a new code is configured.</p>
        </div>

        {revealedCode && (
          <div className="rounded-lg border border-green-100 bg-green-50 px-3.5 py-3">
            <p className="text-xs font-medium text-green-800">One-time reveal</p>
            <p className="mt-1 font-mono text-sm text-green-900 break-all">{revealedCode}</p>
          </div>
        )}

        {error && (
          <p className="text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
