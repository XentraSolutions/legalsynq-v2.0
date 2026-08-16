'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import {
  getTenantProvisioningAction,
  retryProvisioningAction,
  retryVerificationAction,
} from '@/app/tenants/actions';
import type { TenantDetail, ProvisioningFailureStage } from '@/types/control-center';

interface ProvisioningProgressProps {
  tenantId: string;
  initialStatus?: string;
  initialHostname?: string;
  initialError?: string;
  autoRetry?: boolean;
  pollIntervalMs?: number;
  maxPolls?: number;
  maxRetries?: number;
  onSettled?: (state: ProvisioningProgressState) => void;
}

export interface ProvisioningProgressState {
  status: string;
  hostname?: string;
  error?: string;
  failureStage?: string;
  retryCount: number;
  timedOut: boolean;
}

const ACTIVE_STATUSES = new Set(['Active']);
const PENDING_STATUSES = new Set(['Pending', 'InProgress', 'Provisioned', 'Verifying']);

export function ProvisioningProgress({
  tenantId,
  initialStatus = 'Pending',
  initialHostname,
  initialError,
  autoRetry = true,
  pollIntervalMs = 4000,
  maxPolls = 30,
  maxRetries = 3,
  onSettled,
}: ProvisioningProgressProps) {
  const [state, setState] = useState<ProvisioningProgressState>({
    status: initialStatus,
    hostname: initialHostname,
    error: initialError,
    retryCount: 0,
    timedOut: false,
  });
  const [pollCount, setPollCount] = useState(0);
  const settledRef = useRef(false);
  const onSettledRef = useRef(onSettled);

  useEffect(() => {
    onSettledRef.current = onSettled;
  }, [onSettled]);

  const progress = useMemo(() => {
    if (ACTIVE_STATUSES.has(state.status)) return 100;
    if (state.status === 'Failed' || state.timedOut) return 100;
    return Math.min(92, 8 + Math.round((pollCount / maxPolls) * 84));
  }, [maxPolls, pollCount, state.status, state.timedOut]);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | null = null;
    let currentState: ProvisioningProgressState = {
      status: initialStatus,
      hostname: initialHostname,
      error: initialError,
      retryCount: 0,
      timedOut: false,
    };
    let pollCounter = 0;

    async function retry(detail: TenantDetail) {
      const stage = detail.provisioningFailureStage;
      if (stage === 'DnsVerification' || stage === 'HttpVerification') {
        return retryVerificationAction(tenantId);
      }

      return retryProvisioningAction(tenantId);
    }

    async function tick() {
      if (cancelled || settledRef.current) return;

      pollCounter += 1;
      setPollCount(pollCounter);

      try {
        const detail = await getTenantProvisioningAction(tenantId);
        if (!detail) {
          settle({
            ...currentState,
            status: 'Failed',
            error: 'Tenant provisioning status could not be loaded.',
            timedOut: false,
          });
          return;
        }

        const next: ProvisioningProgressState = {
          status: detail.provisioningStatus ?? 'Pending',
          hostname: detail.hostname ?? currentState.hostname,
          error: detail.provisioningFailureReason,
          failureStage: detail.provisioningFailureStage,
          retryCount: currentState.retryCount,
          timedOut: false,
        };

        if (ACTIVE_STATUSES.has(next.status)) {
          settle(next);
          return;
        }

        if (next.status === 'Failed') {
          if (autoRetry && currentState.retryCount < maxRetries && !detail.isVerificationRetryExhausted) {
            const retryResult = await retry(detail);
            const retryCount = currentState.retryCount + 1;
            currentState = {
              status: retryResult.provisioningStatus,
              hostname: retryResult.hostname ?? next.hostname,
              error: retryResult.error,
              failureStage: retryResult.failureStage ?? next.failureStage,
              retryCount,
              timedOut: false,
            };
            setState(currentState);
            timer = setTimeout(tick, pollIntervalMs);
            return;
          }

          settle({
            ...next,
            error: next.error ?? 'DNS provisioning did not complete. Retry from the tenant detail page.',
          });
          return;
        }

        if (pollCounter >= maxPolls) {
          settle({
            ...next,
            error: next.error ?? 'DNS provisioning is still pending after the wait limit. Retry from the tenant detail page.',
            timedOut: true,
          });
          return;
        }

        currentState = next;
        setState(currentState);
        timer = setTimeout(tick, pollIntervalMs);
      } catch (err) {
        settle({
          ...currentState,
          status: 'Failed',
          error: err instanceof Error ? err.message : 'Provisioning status check failed.',
          timedOut: false,
        });
      }
    }

    function settle(next: ProvisioningProgressState) {
      if (cancelled) return;
      settledRef.current = true;
      currentState = next;
      setState(next);
      onSettledRef.current?.(next);
    }

    settledRef.current = false;
    setState(currentState);
    setPollCount(0);

    if (ACTIVE_STATUSES.has(currentState.status)) {
      settle(currentState);
    } else {
      timer = setTimeout(tick, pollIntervalMs);
    }

    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [autoRetry, initialError, initialHostname, initialStatus, maxPolls, maxRetries, pollIntervalMs, tenantId]);

  const isActive = ACTIVE_STATUSES.has(state.status);
  const isWaiting = PENDING_STATUSES.has(state.status) && !state.timedOut;
  const tone = isActive ? 'green' : state.status === 'Failed' || state.timedOut ? 'red' : 'amber';

  return (
    <div className={`rounded-md border px-4 py-3 ${toneClass(tone)}`}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold">{titleFor(state.status, state.timedOut)}</p>
          {state.hostname && (
            <p className="mt-1 font-mono text-[11px]">
              {state.hostname}
            </p>
          )}
          <p className="mt-1 text-[11px] leading-relaxed">
            {messageFor(state)}
          </p>
        </div>
        {isWaiting && (
          <span className="mt-0.5 h-3.5 w-3.5 shrink-0 rounded-full border-2 border-current border-t-transparent animate-spin" />
        )}
      </div>

      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-white/70">
        <div
          className={`h-full rounded-full transition-all duration-500 ${barClass(tone)}`}
          style={{ width: `${progress}%` }}
        />
      </div>

      <div className="mt-2 flex items-center justify-between text-[11px]">
        <span>{isWaiting ? `Check ${Math.min(pollCount + 1, maxPolls)} of ${maxPolls}` : 'Final status'}</span>
        <span>{state.retryCount > 0 ? `${state.retryCount}/${maxRetries} retries used` : 'No retries used'}</span>
      </div>
    </div>
  );
}

function titleFor(status: string, timedOut: boolean): string {
  if (timedOut) return 'DNS provisioning wait limit reached';
  if (status === 'Active') return 'DNS provisioning complete';
  if (status === 'Failed') return 'DNS provisioning needs attention';
  if (status === 'Verifying') return 'Verifying DNS and tenant portal';
  if (status === 'Provisioned') return 'DNS record created';
  if (status === 'InProgress') return 'Creating DNS record';
  return 'DNS provisioning queued';
}

function messageFor(state: ProvisioningProgressState): string {
  if (state.status === 'Active') return 'The tenant subdomain resolves and the tenant portal is reachable.';
  if (state.error) return state.error;
  if (state.failureStage) return `Current stage: ${formatStage(state.failureStage as ProvisioningFailureStage)}.`;
  return 'This can take a few minutes while DNS propagates.';
}

function formatStage(stage: ProvisioningFailureStage): string {
  const labels: Record<ProvisioningFailureStage, string> = {
    None: 'None',
    DnsProvisioning: 'DNS provisioning',
    DnsVerification: 'DNS verification',
    HttpVerification: 'HTTP verification',
  };
  return labels[stage] ?? stage;
}

function toneClass(tone: 'green' | 'red' | 'amber'): string {
  if (tone === 'green') return 'bg-green-50 border-green-200 text-green-800';
  if (tone === 'red') return 'bg-red-50 border-red-200 text-red-800';
  return 'bg-amber-50 border-amber-200 text-amber-800';
}

function barClass(tone: 'green' | 'red' | 'amber'): string {
  if (tone === 'green') return 'bg-green-500';
  if (tone === 'red') return 'bg-red-500';
  return 'bg-amber-500';
}
