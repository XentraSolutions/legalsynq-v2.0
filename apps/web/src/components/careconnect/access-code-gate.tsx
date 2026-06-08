'use client';

import { useState, useEffect, type FormEvent, type ReactNode } from 'react';
import Image from 'next/image';
import { apiClient, ApiError } from '@/lib/api-client';

const STORAGE_KEY_PREFIX = 'cc-network-unlocked:';

function accessCodePath(tenantId: string, action: 'status' | 'verify') {
  return `/public/careconnect/access-code/${action}?tenantId=${encodeURIComponent(tenantId)}`;
}

interface Props {
  tenantId: string;
  children: ReactNode;
}

interface AccessCodeStatusResponse {
  configured: boolean;
  version: number;
}

interface AccessCodeVerifyResponse extends AccessCodeStatusResponse {
  ok: boolean;
}

export function AccessCodeGate({ tenantId, children }: Props) {
  const [unlocked,  setUnlocked]  = useState(false);
  const [ready,     setReady]     = useState(false);
  const [code,      setCode]      = useState('');
  const [error,     setError]     = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [configured, setConfigured] = useState<boolean | null>(null);
  const canEnterCode = configured === true;

  useEffect(() => {
    let cancelled = false;

    async function loadStatus() {
      try {
        const { data } = await apiClient.get<AccessCodeStatusResponse>(accessCodePath(tenantId, 'status'));
        if (cancelled) return;

        const storageKey = `${STORAGE_KEY_PREFIX}${tenantId}`;
        const storedValue = localStorage.getItem(storageKey);

        setConfigured(data.configured);

        if (!data.configured) {
          localStorage.removeItem(storageKey);
          setUnlocked(false);
          setReady(true);
          return;
        }

        let parsed: { tenantId?: string; version?: number; unlocked?: boolean } | null = null;
        if (storedValue) {
          try {
            parsed = JSON.parse(storedValue) as { tenantId?: string; version?: number; unlocked?: boolean };
          } catch {
            localStorage.removeItem(storageKey);
          }
        }

        const matchesVersion =
          parsed?.unlocked === true &&
          parsed.tenantId === tenantId &&
          parsed.version === data.version;

        if (!matchesVersion) {
          localStorage.removeItem(storageKey);
        }

        setUnlocked(matchesVersion);
      } catch {
        if (!cancelled) {
          setError('Unable to verify access right now. Please try again.');
          setConfigured(null);
          setUnlocked(false);
        }
      } finally {
        if (!cancelled) {
          setReady(true);
        }
      }
    }

    void loadStatus();

    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { data } = await apiClient.post<AccessCodeVerifyResponse>(
        accessCodePath(tenantId, 'verify'),
        { code },
      );

      setConfigured(data.configured);

      if (!data.configured) {
        localStorage.removeItem(`${STORAGE_KEY_PREFIX}${tenantId}`);
        setError('This directory is locked until a network access code is configured.');
        setUnlocked(false);
        setCode('');
        return;
      }

      if (!data.ok) {
        setError('Incorrect access code. Please try again.');
        setCode('');
        setUnlocked(false);
        return;
      }

      localStorage.setItem(
        `${STORAGE_KEY_PREFIX}${tenantId}`,
        JSON.stringify({ tenantId, version: data.version, unlocked: true }),
      );
      setError('');
      setUnlocked(true);
      setCode('');
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'Unable to verify access right now. Please try again.',
      );
    }
    finally {
      setSubmitting(false);
    }
  }

  if (!ready) return null;

  return (
    <div className="relative h-full w-full">
      {/* Page content — blurred when locked */}
      <div
        className={unlocked ? 'h-full w-full' : 'h-full w-full blur-sm pointer-events-none select-none'}
        aria-hidden={!unlocked}
      >
        {children}
      </div>

      {/* Access-code modal */}
      {!unlocked && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center"
          style={{ background: 'rgba(10, 16, 30, 0.55)' }}
        >
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4 overflow-hidden">
            {/* Header strip */}
            <div className="bg-gradient-to-r from-slate-900 to-slate-800 px-8 py-6 flex flex-col items-center gap-3">
              <Image
                src="/careconnect-logo.png"
                alt="CareConnect"
                width={140}
                height={36}
                style={{ width: 140, height: 'auto' }}
                className="object-contain"
                priority
              />
              <p className="text-slate-300 text-xs tracking-wide text-center">
                Provider Network
              </p>
            </div>

            {/* Body */}
            <form onSubmit={handleSubmit} className="px-8 py-7 flex flex-col gap-5">
              <div>
                <h2 className="text-base font-semibold text-gray-900 text-center">
                  Enter Access Code
                </h2>
                <p className="text-xs text-gray-500 text-center mt-1">
                  {configured === true
                    ? 'This directory is protected. Please enter the access code provided by your network coordinator.'
                    : configured === false
                      ? 'This directory is locked until a network access code is configured.'
                      : 'Access-code status is unavailable. Try again once the network status can be verified.'}
                </p>
              </div>

              <div className="flex flex-col gap-1.5">
                <label htmlFor="cc-access-code" className="text-xs font-medium text-gray-700">
                  Access Code
                </label>
                <input
                  id="cc-access-code"
                  type="password"
                  autoFocus={canEnterCode}
                  autoComplete="off"
                  value={code}
                  onChange={e => { setCode(e.target.value); setError(''); }}
                  placeholder={canEnterCode ? 'Enter access code' : 'Access code unavailable'}
                  readOnly={!canEnterCode}
                  aria-disabled={!canEnterCode}
                  disabled={!canEnterCode}
                  className={[
                    'w-full rounded-lg border px-3.5 py-2.5 text-sm shadow-sm transition',
                    canEnterCode
                      ? 'border-gray-300 text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-orange-500 focus:border-transparent'
                      : 'cursor-not-allowed border-gray-200 bg-gray-100 text-gray-400 placeholder-gray-400',
                  ].join(' ')}
                />
                {error && (
                  <p className="text-xs text-red-600 flex items-center gap-1 mt-0.5">
                    <i className="ri-error-warning-line" />
                    {error}
                  </p>
                )}
              </div>

              <button
                type="submit"
                disabled={!canEnterCode || !code.trim() || submitting}
                className={[
                  'w-full rounded-lg text-sm font-semibold py-2.5 transition-colors focus:outline-none focus:ring-2 focus:ring-orange-500 focus:ring-offset-2',
                  canEnterCode
                    ? 'bg-orange-500 text-white hover:bg-orange-600 disabled:bg-orange-300'
                    : 'cursor-not-allowed bg-gray-200 text-gray-500',
                ].join(' ')}
              >
                {canEnterCode
                  ? 'Unlock Directory'
                  : configured === false
                    ? 'Access Code Not Configured'
                    : 'Access Code Unavailable'}
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
