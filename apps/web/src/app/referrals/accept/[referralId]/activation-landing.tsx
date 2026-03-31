'use client';

import { useState } from 'react';
import Link from 'next/link';

interface ReferralPublicSummary {
  referralId:       string;
  clientFirstName:  string;
  clientLastName:   string;
  referrerName:     string;
  providerName:     string;
  requestedService: string;
  status:           string;
  isAlreadyAccepted: boolean;
}

interface ActivationLandingProps {
  summary:    ReferralPublicSummary;
  token:      string;
  referralId: string;
}

export function ActivationLanding({ summary, token, referralId }: ActivationLandingProps) {
  const [quickAcceptOpen, setQuickAcceptOpen] = useState(false);
  const [acceptStatus, setAcceptStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [acceptMsg, setAcceptMsg] = useState('');

  const activateUrl = `/referrals/activate?referralId=${referralId}&token=${encodeURIComponent(token)}`;
  const loginUrl    = `/login?returnTo=${encodeURIComponent(`/careconnect/referrals/${referralId}`)}&reason=referral-view`;

  async function handleDirectAccept() {
    setAcceptStatus('loading');
    setAcceptMsg('');
    try {
      const resp = await fetch(`/api/careconnect/api/referrals/${referralId}/accept-by-token`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ token }),
      });
      if (resp.ok) {
        setAcceptStatus('success');
        setAcceptMsg('Referral accepted. The referring party has been notified.');
      } else if (resp.status === 409) {
        setAcceptStatus('error');
        setAcceptMsg('This referral has already been accepted.');
      } else {
        const data = await resp.json().catch(() => null);
        const detail = data?.detail ?? data?.error ?? '';
        setAcceptStatus('error');
        setAcceptMsg(
          detail.toLowerCase().includes('revoked')
            ? 'This link has been revoked. Please check your inbox for a newer link.'
            : 'This link has expired or is invalid. Please contact the referring party.'
        );
      }
    } catch {
      setAcceptStatus('error');
      setAcceptMsg('Connection error. Please try again.');
    }
  }

  if (acceptStatus === 'success') {
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
            <p className="text-sm text-gray-500">{acceptMsg}</p>
            <p className="mt-4 text-xs text-gray-400">
              To track this referral and manage future referrals in one place,{' '}
              <Link href={loginUrl} className="text-primary hover:underline">log in to your account</Link>.
            </p>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
      <div className="max-w-lg w-full space-y-4">

        {/* Referral summary card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="h-1.5 w-full bg-primary" />
          <div className="px-6 py-5">
            <div className="flex items-start gap-3 mb-5">
              <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center shrink-0">
                <svg className="w-5 h-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                    d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
              </div>
              <div>
                <h1 className="text-lg font-semibold text-gray-900">Referral Received</h1>
                <p className="text-sm text-gray-500">You have a new referral through LegalSynq CareConnect</p>
              </div>
            </div>

            {/* Referral details */}
            <div className="bg-gray-50 border border-gray-200 rounded-lg divide-y divide-gray-200">
              {summary.clientFirstName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Client</span>
                  <span className="text-sm text-gray-900 font-medium text-right">
                    {summary.clientFirstName} {summary.clientLastName}
                  </span>
                </div>
              )}
              {summary.referrerName && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Referred by</span>
                  <span className="text-sm text-gray-900 text-right">{summary.referrerName}</span>
                </div>
              )}
              {summary.requestedService && (
                <div className="px-4 py-3 flex justify-between gap-4">
                  <span className="text-xs font-medium text-gray-500 uppercase tracking-wide shrink-0">Service</span>
                  <span className="text-sm text-gray-900 text-right">{summary.requestedService}</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Benefits + activation CTA card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-5 space-y-4">
          <div>
            <h2 className="text-sm font-semibold text-gray-900 mb-1">Activate your CareConnect account</h2>
            <p className="text-sm text-gray-500 leading-relaxed">
              Create your free account to accept this referral and manage future referrals in one place.
            </p>
          </div>

          <ul className="space-y-2">
            {[
              'Accept and track referrals from a single dashboard',
              'Receive notifications when new referrals are sent to you',
              'View referral history and client details securely',
              'Coordinate directly with referring firms',
            ].map((benefit) => (
              <li key={benefit} className="flex items-start gap-2 text-sm text-gray-600">
                <svg className="w-4 h-4 text-green-500 mt-0.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
                </svg>
                {benefit}
              </li>
            ))}
          </ul>

          {/* Primary CTA */}
          <Link
            href={activateUrl}
            className="block w-full bg-primary text-white text-sm font-medium text-center py-2.5 rounded-lg hover:opacity-90 transition-opacity"
          >
            Activate &amp; Accept Referral
          </Link>

          {/* Secondary CTA */}
          <div className="text-center">
            <Link href={loginUrl} className="text-sm text-primary hover:underline font-medium">
              Already have an account? Log in
            </Link>
          </div>

          {/* Tertiary: direct accept (deemphasised) */}
          <div className="border-t border-gray-100 pt-3">
            <button
              onClick={() => setQuickAcceptOpen(v => !v)}
              className="text-xs text-gray-400 hover:text-gray-600 transition-colors flex items-center gap-1 mx-auto"
            >
              {quickAcceptOpen ? '▾' : '▸'} Accept without creating an account
            </button>

            {quickAcceptOpen && (
              <div className="mt-3 space-y-2">
                <p className="text-xs text-gray-500 text-center leading-relaxed">
                  You can accept this specific referral without an account, but you won&apos;t be able to track it or receive future referrals digitally.
                </p>

                {acceptStatus === 'error' && (
                  <div className="bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">
                    {acceptMsg}
                  </div>
                )}

                <button
                  onClick={handleDirectAccept}
                  disabled={acceptStatus === 'loading'}
                  className="w-full border border-gray-300 text-gray-600 text-xs font-medium py-2 rounded-lg hover:bg-gray-50 disabled:opacity-60 transition-colors"
                >
                  {acceptStatus === 'loading' ? 'Accepting…' : 'Accept referral (no account)'}
                </button>
              </div>
            )}
          </div>
        </div>

      </div>
    </main>
  );
}
