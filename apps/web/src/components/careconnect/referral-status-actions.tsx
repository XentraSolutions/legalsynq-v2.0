'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { ReferralDetail } from '@/types/careconnect';

interface ReferralStatusActionsProps {
  referral:   ReferralDetail;
  isReceiver: boolean;
  isReferrer: boolean;
}

/**
 * Inline status-action buttons for a referral detail page.
 *
 * Receiver (provider):
 *   - New / Received / Contacted → Accept | Decline
 *
 * Referrer (law firm):
 *   - Non-terminal statuses → Cancel
 *
 * Uses PUT /api/referrals/{id} which routes through ReferralWorkflowRules on the backend.
 * The backend enforces allowed transitions; we only show contextually-relevant buttons.
 */
export function ReferralStatusActions({ referral, isReceiver, isReferrer }: ReferralStatusActionsProps) {
  const router = useRouter();

  const [optimisticStatus, setOptimisticStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState<string | null>(null);
  const [error,   setError]   = useState<string | null>(null);
  const [notes,   setNotes]   = useState('');
  const [showDeclineNotes, setShowDeclineNotes] = useState(false);

  const currentStatus = optimisticStatus ?? referral.status;

  const isTerminal = ['Completed', 'Cancelled', 'Declined'].includes(currentStatus);
  if (isTerminal) return null;

  async function doUpdate(toStatus: string, notesValue?: string) {
    setLoading(toStatus);
    setError(null);
    setOptimisticStatus(toStatus);

    try {
      await careConnectApi.referrals.update(referral.id, {
        requestedService: referral.requestedService,
        urgency:          referral.urgency,
        status:           toStatus,
        notes:            notesValue || undefined,
      });
      router.refresh();
    } catch (err) {
      setOptimisticStatus(null);
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to update this referral.'); return; }
        setError(err.message);
      } else {
        setError('Failed to update referral status. Please try again.');
      }
    } finally {
      setLoading(null);
    }
  }

  const canAccept  = isReceiver && ['New', 'Received', 'Contacted'].includes(currentStatus);
  const canDecline = isReceiver && ['New', 'Received', 'Contacted', 'Accepted'].includes(currentStatus);
  const canCancel  = (isReferrer || isReceiver) && !['Completed', 'Cancelled', 'Declined'].includes(currentStatus);

  if (!canAccept && !canDecline && !canCancel) return null;

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Receiver: Accept / Decline inline */}
      {(canAccept || canDecline) && (
        <div className="space-y-3">
          <div className="flex items-center gap-3 flex-wrap">
            {canAccept && (
              <button
                onClick={() => doUpdate('Accepted')}
                disabled={!!loading}
                className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-green-700 disabled:opacity-60 transition-colors"
              >
                {loading === 'Accepted' ? 'Accepting…' : 'Accept Referral'}
              </button>
            )}

            {canDecline && !showDeclineNotes && (
              <button
                onClick={() => setShowDeclineNotes(true)}
                disabled={!!loading}
                className="border border-red-300 text-red-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-red-50 disabled:opacity-60 transition-colors"
              >
                Decline
              </button>
            )}
          </div>

          {/* Decline with optional notes */}
          {showDeclineNotes && (
            <div className="space-y-2 border border-red-100 rounded-md p-3 bg-red-50">
              <label className="block text-xs font-medium text-red-700">
                Reason for declining (optional)
              </label>
              <textarea
                value={notes}
                onChange={e => setNotes(e.target.value)}
                rows={2}
                placeholder="Let the referring party know why…"
                className="w-full border border-red-200 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none bg-white"
              />
              <div className="flex items-center gap-2">
                <button
                  onClick={() => doUpdate('Declined', notes)}
                  disabled={!!loading}
                  className="bg-red-600 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-red-700 disabled:opacity-60 transition-colors"
                >
                  {loading === 'Declined' ? 'Declining…' : 'Confirm Decline'}
                </button>
                <button
                  onClick={() => { setShowDeclineNotes(false); setNotes(''); }}
                  disabled={!!loading}
                  className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Referrer or Receiver: Cancel referral */}
      {canCancel && (
        <div className="pt-1 border-t border-gray-100">
          <button
            onClick={() => doUpdate('Cancelled')}
            disabled={!!loading}
            className="text-sm text-gray-500 hover:text-gray-800 transition-colors disabled:opacity-50"
          >
            {loading === 'Cancelled' ? 'Cancelling…' : 'Cancel Referral'}
          </button>
        </div>
      )}
    </div>
  );
}
