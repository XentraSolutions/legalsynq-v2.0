'use client';

import { useState } from 'react';
import { useParams, useSearchParams } from 'next/navigation';

/**
 * LSCC-005: Public pending-provider referral acceptance page.
 *
 * Accessible without authentication. The `token` query param is an
 * HMAC-signed view token that authorises the acceptance action.
 * Used when the target provider has no Identity org link (OrganizationId = null).
 */

const INVALID_ID = 'invalid';

export default function ReferralAcceptPage() {
  const params       = useParams<{ referralId: string }>();
  const searchParams = useSearchParams();
  const referralId   = params.referralId;
  const token        = searchParams.get('token') ?? '';
  const reason       = searchParams.get('reason') ?? '';

  const [status,  setStatus]  = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [message, setMessage] = useState('');

  if (referralId === INVALID_ID) {
    return (
      <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="text-4xl mb-4">⚠️</div>
          <h1 className="text-xl font-semibold text-gray-900 mb-2">
            Link Expired or Invalid
          </h1>
          <p className="text-sm text-gray-500">
            {reason === 'missing-token'
              ? 'No access token was found in the link.'
              : 'This referral link has expired or is no longer valid. Please contact the referring party for a new link.'}
          </p>
        </div>
      </main>
    );
  }

  async function handleAccept() {
    if (!token) {
      setStatus('error');
      setMessage('Missing access token. Please use the link from your email.');
      return;
    }

    setStatus('loading');
    setMessage('');

    try {
      const resp = await fetch(`/api/careconnect/api/referrals/${referralId}/accept-by-token`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ token }),
      });

      if (resp.ok) {
        setStatus('success');
        setMessage('Your acceptance has been recorded. The referring party will be notified.');
      } else if (resp.status === 401) {
        setStatus('error');
        setMessage('This link has expired or is invalid. Please contact the referring party for a new link.');
      } else if (resp.status === 409) {
        const data = await resp.json().catch(() => null);
        setStatus('error');
        setMessage(data?.error ?? 'This referral has already been processed and cannot be accepted again.');
      } else {
        setStatus('error');
        setMessage('Something went wrong. Please try again or contact the referring party.');
      }
    } catch {
      setStatus('error');
      setMessage('Unable to connect. Please check your internet connection and try again.');
    }
  }

  if (status === 'success') {
    return (
      <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center">
          <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h1 className="text-xl font-semibold text-gray-900 mb-2">Referral Accepted</h1>
          <p className="text-sm text-gray-500">{message}</p>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 p-8">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center shrink-0">
            <svg className="w-5 h-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          </div>
          <div>
            <h1 className="text-lg font-semibold text-gray-900">New Referral Received</h1>
            <p className="text-sm text-gray-500">You have been sent a referral</p>
          </div>
        </div>

        <p className="text-sm text-gray-600 mb-6">
          A referral has been sent to you through LegalSynq CareConnect. By clicking
          &ldquo;Accept Referral&rdquo; below, you confirm that you will handle this referral
          and contact the client to schedule an appointment.
        </p>

        {status === 'error' && (
          <div className="mb-4 bg-red-50 border border-red-200 rounded-md px-4 py-3 text-sm text-red-700">
            {message}
          </div>
        )}

        <button
          onClick={handleAccept}
          disabled={status === 'loading' || !token}
          className="w-full bg-primary text-white font-medium py-2.5 rounded-lg hover:opacity-90 disabled:opacity-60 transition-opacity text-sm"
        >
          {status === 'loading' ? 'Accepting…' : 'Accept Referral'}
        </button>

        <p className="mt-4 text-xs text-gray-400 text-center">
          If you believe this referral was sent to you in error, you may ignore this page.
          The link will expire in 30 days.
        </p>
      </div>
    </main>
  );
}
