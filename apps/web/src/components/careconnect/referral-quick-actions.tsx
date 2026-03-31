'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { buildReferralDetailUrl } from '@/lib/referral-nav';
import type { ReferralSummary } from '@/types/careconnect';

interface ReferralQuickActionsProps {
  referral:   ReferralSummary;
  isReferrer: boolean;
  isReceiver: boolean;
  /** Current list query string (without leading "?") — preserved in the detail URL for back navigation */
  contextQs?: string;
}

const ACTIONABLE_FOR_RECEIVER = ['New', 'Received', 'Contacted'];

export function ReferralQuickActions({ referral, isReferrer, isReceiver, contextQs = '' }: ReferralQuickActionsProps) {
  const router          = useRouter();
  const { show: toast } = useToast();

  const [busy,   setBusy]   = useState<string | null>(null);
  const [confirming, setConfirming] = useState<string | null>(null);

  const canAccept  = isReceiver && ACTIONABLE_FOR_RECEIVER.includes(referral.status);
  const canResend  = isReferrer && referral.status === 'New';
  const canRevoke  = isReferrer;

  async function handleAccept() {
    setBusy('accept');
    try {
      await careConnectApi.referrals.update(referral.id, {
        requestedService: referral.requestedService,
        urgency:          referral.urgency,
        status:           'Accepted',
      });
      toast('Referral accepted.', 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to accept referral.';
      toast(msg, 'error');
    } finally {
      setBusy(null);
    }
  }

  async function handleResend() {
    setBusy('resend');
    try {
      await careConnectApi.referrals.resendEmail(referral.id);
      toast('Notification email resent.', 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to resend email.';
      toast(msg, 'error');
    } finally {
      setBusy(null);
    }
  }

  async function handleRevokeConfirm() {
    setBusy('revoke');
    setConfirming(null);
    try {
      await careConnectApi.referrals.revokeToken(referral.id);
      toast('Referral token revoked.', 'success');
      router.refresh();
    } catch (err) {
      const msg = err instanceof ApiError ? err.message : 'Failed to revoke token.';
      toast(msg, 'error');
    } finally {
      setBusy(null);
    }
  }

  if (confirming === 'revoke') {
    return (
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs text-gray-600">Revoke the current email link?</span>
        <button
          onClick={handleRevokeConfirm}
          disabled={!!busy}
          className="text-xs font-medium px-2.5 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-60 transition-colors"
        >
          Yes, Revoke
        </button>
        <button
          onClick={() => setConfirming(null)}
          className="text-xs text-gray-500 hover:text-gray-700 transition-colors"
        >
          Cancel
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 flex-wrap">
      {/* View — always present; carries context so detail page back-link is correct */}
      <Link
        href={buildReferralDetailUrl(referral.id, contextQs)}
        className="text-xs font-medium px-2.5 py-1 border border-gray-200 text-gray-700 rounded hover:bg-gray-50 transition-colors whitespace-nowrap"
      >
        View
      </Link>

      {/* Receiver: Accept */}
      {canAccept && (
        <button
          onClick={handleAccept}
          disabled={!!busy}
          className="text-xs font-medium px-2.5 py-1 bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-60 transition-colors whitespace-nowrap"
        >
          {busy === 'accept' ? 'Accepting…' : 'Accept'}
        </button>
      )}

      {/* Referrer: Resend Email */}
      {canResend && (
        <button
          onClick={handleResend}
          disabled={!!busy}
          className="text-xs font-medium px-2.5 py-1 border border-primary text-primary rounded hover:bg-primary/5 disabled:opacity-60 transition-colors whitespace-nowrap"
        >
          {busy === 'resend' ? 'Sending…' : 'Resend Email'}
        </button>
      )}

      {/* Referrer: Revoke Link */}
      {canRevoke && (
        <button
          onClick={() => setConfirming('revoke')}
          disabled={!!busy}
          className="text-xs font-medium px-2.5 py-1 border border-gray-200 text-gray-500 rounded hover:bg-gray-50 hover:border-red-200 hover:text-red-600 disabled:opacity-60 transition-colors whitespace-nowrap"
        >
          Revoke Link
        </button>
      )}
    </div>
  );
}
