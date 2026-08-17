'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  retryProvisioningAction,
  retryVerificationAction,
} from '@/app/tenants/actions';
import { ProvisioningProgress } from './provisioning-progress';
import type { ProvisioningFailureStage, ProvisioningStatus } from '@/types/control-center';

interface RetryDnsSetupButtonProps {
  tenantId: string;
  status?: ProvisioningStatus;
  failureStage?: ProvisioningFailureStage;
  hostname?: string;
}

function shouldRetryVerification(
  status?: ProvisioningStatus,
  stage?: ProvisioningFailureStage,
  hostname?: string,
): boolean {
  if (stage === 'DnsVerification' || stage === 'HttpVerification') return true;
  return status === 'Failed' && Boolean(hostname);
}

export function RetryDnsSetupButton({ tenantId, status, failureStage, hostname }: RetryDnsSetupButtonProps) {
  const router = useRouter();
  const [isPending, setIsPending] = useState(false);
  const [result, setResult] = useState<{
    success: boolean;
    status?: string;
    error?: string;
  } | null>(null);
  const [tracking, setTracking] = useState<{
    status: string;
    error?: string;
  } | null>(null);

  async function handleRetry() {
    setIsPending(true);
    setResult(null);
    setTracking(null);

    try {
      const res = shouldRetryVerification(status, failureStage, hostname)
        ? await retryVerificationAction(tenantId)
        : await retryProvisioningAction(tenantId);

      setResult({
        success: res.success,
        status: res.provisioningStatus,
        error: res.error,
      });
      setTracking({
        status: res.success ? res.provisioningStatus : 'Verifying',
        error: res.error,
      });
    } catch {
      setResult({ success: false, error: 'Unexpected error during DNS setup retry.' });
    } finally {
      setIsPending(false);
    }
  }

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={handleRetry}
        disabled={isPending || status === 'Active'}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isPending ? (
          <>
            <span className="h-3 w-3 rounded-full border-2 border-white/60 border-t-transparent animate-spin" />
            Retrying...
          </>
        ) : (
          'Retry DNS Setup'
        )}
      </button>

      {tracking ? (
        <ProvisioningProgress
          tenantId={tenantId}
          initialStatus={tracking.status}
          initialHostname={hostname}
          initialError={tracking.error}
          autoRetry={false}
          onSettled={() => router.refresh()}
        />
      ) : (isPending || result) && (
        <div className={`rounded-md border px-4 py-3 ${progressToneClass(isPending, result)}`}>
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-xs font-semibold">
                {progressTitle(isPending, result)}
              </p>
              <p className="mt-1 text-[11px] leading-relaxed">
                {progressMessage(isPending, result)}
              </p>
            </div>
            {isPending && (
              <span className="mt-0.5 h-3.5 w-3.5 shrink-0 rounded-full border-2 border-current border-t-transparent animate-spin" />
            )}
          </div>

          <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-white/70">
            <div
              className={`h-full rounded-full transition-all duration-500 ${progressBarClass(isPending, result)}`}
              style={{ width: `${progressWidth(isPending, result)}%` }}
            />
          </div>
        </div>
      )}
    </div>
  );
}

function progressWidth(
  isPending: boolean,
  result: { success: boolean; status?: string; error?: string } | null,
): number {
  if (isPending) return 70;
  if (result) return 100;
  return 0;
}

function progressTitle(
  isPending: boolean,
  result: { success: boolean; status?: string; error?: string } | null,
): string {
  if (isPending) return 'Retrying DNS setup';
  if (result?.success) return 'DNS setup retry complete';
  return 'DNS setup retry needs attention';
}

function progressMessage(
  isPending: boolean,
  result: { success: boolean; status?: string; error?: string } | null,
): string {
  if (isPending) return 'The selected DNS provisioning or verification step is running.';
  if (result?.error) return result.error;
  if (result?.status) return `Identity returned status: ${result.status}.`;
  return 'The retry completed without a detailed status.';
}

function progressToneClass(
  isPending: boolean,
  result: { success: boolean; status?: string; error?: string } | null,
): string {
  if (isPending) return 'bg-amber-50 border-amber-200 text-amber-800';
  if (result?.success) return 'bg-green-50 border-green-200 text-green-800';
  return 'bg-red-50 border-red-200 text-red-800';
}

function progressBarClass(
  isPending: boolean,
  result: { success: boolean; status?: string; error?: string } | null,
): string {
  if (isPending) return 'bg-amber-500';
  if (result?.success) return 'bg-green-500';
  return 'bg-red-500';
}
