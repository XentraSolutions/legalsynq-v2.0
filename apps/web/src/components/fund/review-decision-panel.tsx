'use client';

import { useState, type FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import { fundApi } from '@/lib/fund-api';
import { ApiError } from '@/lib/api-client';
import type { FundingApplicationDetail } from '@/types/fund';

interface ReviewDecisionPanelProps {
  application: FundingApplicationDetail;
  onUpdated:   (updated: FundingApplicationDetail) => void;
}

type Mode = 'idle' | 'approving' | 'denying';

/**
 * Panel shown to SYNQFUND_FUNDER on Submitted or InReview applications.
 *
 * - Submitted:  shows "Begin Review" button only.
 * - InReview:   shows Approve / Deny actions.
 */
export function ReviewDecisionPanel({ application, onUpdated }: ReviewDecisionPanelProps) {
  const router = useRouter();
  const [mode,           setMode]           = useState<Mode>('idle');
  const [approvedAmount, setApprovedAmount] = useState('');
  const [approvalTerms,  setApprovalTerms]  = useState('');
  const [denialReason,   setDenialReason]   = useState('');
  const [loading,        setLoading]        = useState(false);
  const [error,          setError]          = useState<string | null>(null);

  async function handleBeginReview() {
    setError(null);
    setLoading(true);
    try {
      const { data } = await fundApi.applications.beginReview(application.id);
      onUpdated(data);
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleApprove(e: FormEvent) {
    e.preventDefault();
    const amount = parseFloat(approvedAmount);
    if (!approvedAmount || isNaN(amount) || amount <= 0) {
      setError('Please enter a valid approved amount greater than zero.');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const { data } = await fundApi.applications.approve(application.id, {
        approvedAmount: amount,
        approvalTerms:  approvalTerms.trim() || undefined,
      });
      onUpdated(data);
      setMode('idle');
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  async function handleDeny(e: FormEvent) {
    e.preventDefault();
    if (!denialReason.trim()) {
      setError('Please provide a denial reason.');
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const { data } = await fundApi.applications.deny(application.id, {
        reason: denialReason.trim(),
      });
      onUpdated(data);
      setMode('idle');
    } catch (err) {
      handleApiError(err);
    } finally {
      setLoading(false);
    }
  }

  function handleApiError(err: unknown) {
    if (err instanceof ApiError) {
      if (err.isUnauthorized) { router.push('/login'); return; }
      if (err.isConflict)     { setError('This action is not valid for the application\'s current state.'); return; }
      if (err.isForbidden)    { setError('You do not have permission to perform this action.'); return; }
      setError(err.message);
    } else {
      setError('An unexpected error occurred.');
    }
  }

  const { status } = application;

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <h3 className="text-sm font-semibold text-gray-900 mb-3">Funder Actions</h3>

      {error && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Submitted: only Begin Review */}
      {status === 'Submitted' && mode === 'idle' && (
        <div className="space-y-2">
          <p className="text-sm text-gray-500">
            Start reviewing this application to unlock the approve / deny decisions.
          </p>
          <button
            onClick={handleBeginReview}
            disabled={loading}
            className="bg-primary text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
          >
            {loading ? 'Starting review…' : 'Begin Review'}
          </button>
        </div>
      )}

      {/* InReview: approve or deny */}
      {status === 'InReview' && mode === 'idle' && (
        <div className="flex items-center gap-3">
          <button
            onClick={() => { setMode('approving'); setError(null); }}
            className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Approve
          </button>
          <button
            onClick={() => { setMode('denying'); setError(null); }}
            className="bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:opacity-90 transition-opacity"
          >
            Deny
          </button>
        </div>
      )}

      {/* Approve form */}
      {mode === 'approving' && (
        <form onSubmit={handleApprove} className="space-y-3 mt-2">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Approved amount (USD) <span className="text-red-500">*</span>
            </label>
            <input
              type="number"
              min="0.01"
              step="0.01"
              value={approvedAmount}
              onChange={e => setApprovedAmount(e.target.value)}
              placeholder="e.g. 25000"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Approval terms <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <textarea
              value={approvalTerms}
              onChange={e => setApprovalTerms(e.target.value)}
              rows={3}
              placeholder="Repayment rate, conditions, etc.…"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 resize-none"
            />
          </div>
          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={loading}
              className="bg-green-600 text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
            >
              {loading ? 'Approving…' : 'Confirm Approval'}
            </button>
            <button type="button" onClick={() => { setMode('idle'); setError(null); }}
              className="text-sm text-gray-500 hover:text-gray-800">
              Cancel
            </button>
          </div>
        </form>
      )}

      {/* Deny form */}
      {mode === 'denying' && (
        <form onSubmit={handleDeny} className="space-y-3 mt-2">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Denial reason <span className="text-red-500">*</span>
            </label>
            <textarea
              value={denialReason}
              onChange={e => setDenialReason(e.target.value)}
              rows={4}
              placeholder="Explain why the application is being denied…"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-500 resize-none"
            />
          </div>
          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={loading}
              className="bg-red-600 text-white text-sm font-medium px-5 py-2 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
            >
              {loading ? 'Denying…' : 'Confirm Denial'}
            </button>
            <button type="button" onClick={() => { setMode('idle'); setError(null); }}
              className="text-sm text-gray-500 hover:text-gray-800">
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
