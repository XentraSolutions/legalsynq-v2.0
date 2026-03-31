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
}

interface ActivationFormProps {
  summary:    ReferralPublicSummary;
  token:      string;
  referralId: string;
}

export function ActivationForm({ summary, token, referralId }: ActivationFormProps) {
  const [name,   setName]   = useState('');
  const [email,  setEmail]  = useState('');
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [error,  setError]  = useState('');

  const loginUrl   = `/login?returnTo=${encodeURIComponent(`/careconnect/referrals/${referralId}`)}&reason=referral-view`;
  const clientName = [summary.clientFirstName, summary.clientLastName].filter(Boolean).join(' ');

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim() || !email.trim()) {
      setError('Please enter your name and email address.');
      return;
    }
    setStatus('loading');
    setError('');

    try {
      const resp = await fetch(`/api/careconnect/api/referrals/${referralId}/track-funnel`, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify({ token, eventType: 'ActivationStarted' }),
      });

      if (resp.ok || resp.status === 200) {
        setStatus('success');
      } else {
        setStatus('error');
        setError('Something went wrong. Please try again or contact the referring party.');
      }
    } catch {
      setStatus('error');
      setError('Connection error. Please check your internet connection and try again.');
    }
  }

  if (status === 'success') {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="h-1.5 w-full bg-green-400" />
        <div className="p-8 text-center">
          <div className="w-14 h-14 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-7 h-7 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold text-gray-900 mb-2">Activation Request Received</h2>
          <p className="text-sm text-gray-500 leading-relaxed mb-4">
            Thank you, <strong>{name}</strong>. Your activation request has been submitted.
          </p>
          <p className="text-sm text-gray-500 leading-relaxed mb-6">
            A member of our team will set up your account and send you login details shortly.
            Once your account is active, you can log in and accept the referral
            {clientName ? ` for ${clientName}` : ''} directly from your dashboard.
          </p>
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 text-sm text-blue-800 mb-6">
            <strong>Already have an account?</strong>{' '}
            <Link href={loginUrl} className="text-primary hover:underline font-medium">
              Log in now
            </Link>{' '}
            to view this referral immediately.
          </div>
          <p className="text-xs text-gray-400">
            If you have any questions, please contact the referring party directly.
          </p>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-xl shadow-sm border border-gray-200 px-6 py-5 space-y-4">
      <div>
        <h2 className="text-sm font-semibold text-gray-900 mb-1">Your details</h2>
        <p className="text-xs text-gray-500">
          We&apos;ll use these to set up your account and contact you once it&apos;s ready.
        </p>
      </div>

      <div>
        <label htmlFor="activate-name" className="block text-xs font-medium text-gray-700 mb-1">
          Full name <span className="text-red-500">*</span>
        </label>
        <input
          id="activate-name"
          type="text"
          required
          value={name}
          onChange={e => setName(e.target.value)}
          placeholder="Your full name"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
      </div>

      <div>
        <label htmlFor="activate-email" className="block text-xs font-medium text-gray-700 mb-1">
          Email address <span className="text-red-500">*</span>
        </label>
        <input
          id="activate-email"
          type="email"
          required
          value={email}
          onChange={e => setEmail(e.target.value)}
          placeholder="you@yourpractice.com"
          className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary"
        />
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-xs text-red-700">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={status === 'loading'}
        className="w-full bg-primary text-white text-sm font-medium py-2.5 rounded-lg hover:opacity-90 disabled:opacity-60 transition-opacity"
      >
        {status === 'loading' ? 'Submitting…' : 'Request Account Activation'}
      </button>

      <p className="text-xs text-gray-400 text-center">
        Already have an account?{' '}
        <Link href={loginUrl} className="text-primary hover:underline">
          Log in instead
        </Link>
      </p>
    </form>
  );
}
