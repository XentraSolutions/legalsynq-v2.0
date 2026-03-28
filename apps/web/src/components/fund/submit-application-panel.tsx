'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { fundApi } from '@/lib/fund-api';
import { ApiError } from '@/lib/api-client';
import type { FundingApplicationDetail } from '@/types/fund';

interface SubmitApplicationPanelProps {
  application: FundingApplicationDetail;
  onUpdated:   (updated: FundingApplicationDetail) => void;
}

/**
 * Inline panel shown to SYNQFUND_REFERRER on a Draft application.
 *
 * Allows the law firm to optionally specify a funder org ID before
 * transitioning Draft → Submitted.
 *
 * Phase 1: funder org is a free-text UUID field.
 * Phase 2: replace with an org-picker that queries /fund/api/funders.
 */
export function SubmitApplicationPanel({ application, onUpdated }: SubmitApplicationPanelProps) {
  const router  = useRouter();
  const [funderId, setFunderId] = useState(application.funderId ?? '');
  const [loading, setLoading]   = useState(false);
  const [error,   setError]     = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const { data } = await fundApi.applications.submit(application.id, {
        funderId: funderId.trim() || undefined,
      });
      onUpdated(data);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isConflict)     { setError('This application cannot be submitted from its current state.'); return; }
        if (err.isForbidden)    { setError('You do not have permission to submit this application.'); return; }
        setError(err.message);
      } else {
        setError('An unexpected error occurred.');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Submit Application</h3>
      <p className="text-sm text-gray-500 mb-4">
        Submitting will send this application to the funder for review.
        You will no longer be able to edit it.
      </p>

      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Funder org ID <span className="text-gray-400 font-normal">(optional)</span>
          </label>
          <input
            type="text"
            value={funderId}
            onChange={e => setFunderId(e.target.value)}
            placeholder="UUID of the funder organisation…"
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          />
          <p className="mt-1 text-xs text-gray-400">
            Phase 2 will replace this with an org picker.
          </p>
        </div>

        <div className="flex items-center gap-3 pt-1">
          <button
            type="submit"
            disabled={loading}
            className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
          >
            {loading ? 'Submitting…' : 'Submit to Funder'}
          </button>
        </div>
      </form>
    </div>
  );
}
