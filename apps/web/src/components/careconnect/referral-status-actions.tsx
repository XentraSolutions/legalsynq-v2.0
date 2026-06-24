'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import { usePermission } from '@/hooks/use-permission';
import { PermissionCodes } from '@/lib/permission-codes';
import { ForbiddenBanner } from '@/components/ui/forbidden-banner';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons } from '@/lib/disabled-reasons';
import type { ReferralDetail } from '@/types/careconnect';

interface ReferralStatusActionsProps {
  referral:   ReferralDetail;
  isReceiver: boolean;
  isReferrer: boolean;
}

const STATUS_LABELS: Record<string, string> = {
  Accepted:   'Referral accepted.',
  Completed:  'Referral marked as completed.',
  Declined:   'Referral declined.',
  Cancelled:  'Referral cancelled.',
};

export function ReferralStatusActions({ referral, isReceiver, isReferrer }: ReferralStatusActionsProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  const canAcceptPerm       = usePermission(PermissionCodes.CC.ReferralAccept);
  const canDeclinePerm      = usePermission(PermissionCodes.CC.ReferralDecline);
  const canCancelPerm       = usePermission(PermissionCodes.CC.ReferralCancel);
  const canUpdateStatusPerm = usePermission(PermissionCodes.CC.ReferralUpdateStatus);

  const [optimisticStatus, setOptimisticStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState<string | null>(null);
  const [error,   setError]   = useState<string | null>(null);
  const [declineNotes, setDeclineNotes] = useState('');

  const [showDeclineNotes,  setShowDeclineNotes]  = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);

  const currentStatus = optimisticStatus ?? referral.status;
  const isTerminal    = ['Completed', 'Cancelled', 'Declined'].includes(currentStatus);
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
        ...(toStatus === 'Declined' && notesValue ? { declineNotes: notesValue } : {}),
      });
      showToast(STATUS_LABELS[toStatus] ?? 'Referral updated.', 'success');
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
      showToast('Failed to update referral.', 'error');
    } finally {
      setLoading(null);
    }
  }

  // New/NewOpened: Accept + Decline
  const roleCanAccept  = isReceiver && ['New', 'NewOpened', 'Received', 'Contacted'].includes(currentStatus);
  const roleCanDecline = isReceiver && ['New', 'NewOpened', 'Received', 'Contacted'].includes(currentStatus);

  // Accepted/InProgress: Completed + Cancel
  const roleCanComplete = isReceiver && (currentStatus === 'Accepted' || currentStatus === 'InProgress');
  const receiverCanCancel = isReceiver && (currentStatus === 'Accepted' || currentStatus === 'InProgress');
  const referrerCanCancel = isReferrer && !['Completed', 'Cancelled', 'Declined'].includes(currentStatus);
  const roleCanCancel     = receiverCanCancel || referrerCanCancel;

  const canAccept   = roleCanAccept   && canAcceptPerm;
  const canDecline  = roleCanDecline  && canDeclinePerm;
  const canComplete = roleCanComplete && canAcceptPerm;
  const canCancel   = roleCanCancel   && (canCancelPerm || (isReceiver && canAcceptPerm));

  const hasAnyRoleAccess = roleCanAccept || roleCanDecline || roleCanComplete || roleCanCancel;
  if (!hasAnyRoleAccess) return null;

  const hasAnyPermAccess = canAccept || canDecline || canComplete || canCancel;
  if (!hasAnyPermAccess) {
    return (
      <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>
        <ForbiddenBanner action="manage this referral" />
      </div>
    );
  }

  const showNewOpenedSection  = roleCanAccept || roleCanDecline;
  const showCompleteOrCancel = roleCanComplete || roleCanCancel;

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* New/NewOpened: Accept + Decline */}
      {showNewOpenedSection && (
        <div className="space-y-3">
          <div className="flex items-center gap-3 flex-wrap">
            {roleCanAccept && !showDeclineNotes && (
              <PermissionTooltip
                show={!canAccept}
                message={DisabledReasons.noPermission('accept this referral').message}
              >
                <button
                  onClick={() => doUpdate('Accepted')}
                  disabled={!!loading || !canAccept}
                  className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-green-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  {loading === 'Accepted' ? 'Accepting…' : 'Accept Referral'}
                </button>
              </PermissionTooltip>
            )}

            {roleCanDecline && !showDeclineNotes && (
              <PermissionTooltip
                show={!canDecline}
                message={DisabledReasons.noPermission('decline this referral').message}
              >
                <button
                  onClick={() => setShowDeclineNotes(true)}
                  disabled={!!loading || !canDecline}
                  className="border border-red-300 text-red-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-red-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Decline
                </button>
              </PermissionTooltip>
            )}
          </div>

          {showDeclineNotes && (
            <div className="space-y-2 border border-red-100 rounded-md p-3 bg-red-50">
              <label className="block text-xs font-medium text-red-700">
                Reason for declining (optional)
              </label>
              <textarea
                value={declineNotes}
                onChange={e => setDeclineNotes(e.target.value)}
                rows={2}
                placeholder="Let the referring party know why…"
                className="w-full border border-red-200 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none bg-white"
              />
              <div className="flex items-center gap-2">
                <button
                  onClick={() => doUpdate('Declined', declineNotes)}
                  disabled={!!loading}
                  className="bg-red-600 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-red-700 disabled:opacity-60 transition-colors"
                >
                  {loading === 'Declined' ? 'Declining…' : 'Confirm Decline'}
                </button>
                <button
                  onClick={() => { setShowDeclineNotes(false); setDeclineNotes(''); }}
                  disabled={!!loading}
                  className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                >
                  Go back
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Completed + Cancel (receivers from Accepted; referrers from any non-terminal) */}
      {showCompleteOrCancel && (
        <div className="space-y-3">
          {!showCancelConfirm && (
            <div className="flex items-center gap-3 flex-wrap">
              {roleCanComplete && (
                <PermissionTooltip
                  show={!canComplete}
                  message={DisabledReasons.noPermission('complete this referral').message}
                >
                  <button
                    onClick={() => doUpdate('Completed')}
                    disabled={!!loading || !canComplete}
                    className="bg-green-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-green-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    {loading === 'Completed' ? 'Completing…' : 'Mark as Completed'}
                  </button>
                </PermissionTooltip>
              )}

              {roleCanCancel && (
                <PermissionTooltip
                  show={!canCancel}
                  message={DisabledReasons.noPermission('cancel this referral').message}
                >
                  <button
                    onClick={() => { if (canCancel) setShowCancelConfirm(true); }}
                    disabled={!!loading || !canCancel}
                    className="bg-gray-600 text-white text-sm font-medium px-4 py-2 rounded-md hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    Cancel Referral
                  </button>
                </PermissionTooltip>
              )}
            </div>
          )}

          {showCancelConfirm && (
            <div className="space-y-2 border border-gray-200 rounded-md p-3 bg-gray-50">
              <p className="text-sm font-medium text-gray-800">
                Cancel this referral?
              </p>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => doUpdate('Cancelled')}
                  disabled={!!loading}
                  className="bg-gray-700 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-gray-900 disabled:opacity-60 transition-colors"
                >
                  {loading === 'Cancelled' ? 'Cancelling…' : 'Yes, Cancel'}
                </button>
                <button
                  onClick={() => setShowCancelConfirm(false)}
                  disabled={!!loading}
                  className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                >
                  Keep Referral
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
