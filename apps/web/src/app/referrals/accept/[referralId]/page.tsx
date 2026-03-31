'use client';

import { useState } from 'react';
import { useParams, useSearchParams } from 'next/navigation';

/**
 * LSCC-005 / LSCC-005-01: Public pending-provider referral acceptance page.
 *
 * Accessible without authentication. The `token` query param is an
 * HMAC-signed view token that authorises the acceptance action.
 * Used when the target provider has no Identity org link (OrganizationId = null).
 *
 * The `reason` param on the invalid screen:
 *   missing-token        → no token was present in the URL
 *   expired-or-invalid   → token failed HMAC / expiry validation
 *   revoked              → token's version doesn't match referral's current version
 */

const INVALID_ID = 'invalid';

function InvalidScreen({ reason }: { reason: string }) {
  const isRevoked     = reason === 'revoked';
  const isMissing     = reason === 'missing-token';

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        {/* Colour band at top */}
        <div className={`h-1.5 w-full ${isRevoked ? 'bg-orange-400' : 'bg-red-400'}`} />

        <div className="p-8 text-center">
          {/* Icon */}
          <div className="w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-5
                          bg-gray-100">
            {isRevoked ? (
              // Lock icon for revoked links
              <svg className="w-7 h-7 text-orange-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M12 15v2m0 0v2m0-2h2m-2 0H10m7-7V9a5 5 0 00-10 0v1M5 12h14a1 1 0 011 1v7a1 1 0 01-1 1H5a1 1 0 01-1-1v-7a1 1 0 011-1z" />
              </svg>
            ) : (
              // Warning icon for expired / missing
              <svg className="w-7 h-7 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              </svg>
            )}
          </div>

          {/* Heading */}
          <h1 className="text-xl font-semibold text-gray-900 mb-2">
            {isMissing   && 'Link Missing'}
            {isRevoked   && 'Link Revoked'}
            {!isMissing && !isRevoked && 'Link Expired or Invalid'}
          </h1>

          {/* Description */}
          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            {isMissing && (
              'No access token was found in the link. Please use the original email link sent to you.'
            )}
            {isRevoked && (
              'This referral link has been revoked by the sending organisation. ' +
              'A new link may have been sent to you — please check your inbox, ' +
              'or contact the referring party to request a fresh invitation.'
            )}
            {!isMissing && !isRevoked && (
              'This referral link has expired or is no longer valid. ' +
              'Links are valid for 30 days from the date the referral was sent. ' +
              'Please contact the referring party to request a new link.'
            )}
          </p>

          {/* Steps panel */}
          <div className="bg-gray-50 border border-gray-200 rounded-lg p-4 text-left mb-6">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">
              What to do next
            </p>
            <ol className="space-y-2 text-sm text-gray-600">
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">1.</span>
                Check your inbox for a more recent email from the referring party.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">2.</span>
                If you cannot find a newer link, contact the referring party and ask them
                to resend the referral invitation.
              </li>
              <li className="flex gap-2">
                <span className="font-semibold text-gray-400 shrink-0">3.</span>
                If you are an existing platform user, you can{' '}
                <a href="/login" className="text-primary hover:underline">log in</a>
                {' '}to view referrals sent to your organisation.
              </li>
            </ol>
          </div>

          <p className="text-xs text-gray-400">
            If you believe this is an error, please contact your system administrator.
          </p>
        </div>
      </div>
    </main>
  );
}

export default function ReferralAcceptPage() {
  const params       = useParams<{ referralId: string }>();
  const searchParams = useSearchParams();
  const referralId   = params.referralId;
  const token        = searchParams.get('token') ?? '';
  const reason       = searchParams.get('reason') ?? '';

  const [status,  setStatus]  = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [message, setMessage] = useState('');

  if (referralId === INVALID_ID) {
    return <InvalidScreen reason={reason} />;
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
        // LSCC-005-01: could be revoked or expired — the server now distinguishes
        const data = await resp.json().catch(() => null);
        const detail = data?.detail ?? data?.error ?? '';
        const isRevoked = detail.toLowerCase().includes('revoked');
        setStatus('error');
        setMessage(
          isRevoked
            ? 'This referral link has been revoked. Please check your inbox for a newer link, or contact the referring party.'
            : 'This link has expired or is invalid. Please contact the referring party for a new link.'
        );
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
        <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="h-1.5 w-full bg-green-400" />
          <div className="p-8 text-center">
            <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
              <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h1 className="text-xl font-semibold text-gray-900 mb-2">Referral Accepted</h1>
            <p className="text-sm text-gray-500">{message}</p>
            <p className="mt-4 text-xs text-gray-400">
              The referring party has been notified. They will be in touch with next steps.
            </p>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-primary" />
        <div className="p-8">
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
              <p className="text-sm text-gray-500">You have been sent a referral via CareConnect</p>
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
      </div>
    </main>
  );
}
