'use client';

/**
 * UserActions — user account control buttons with confirmation dialogs.
 *
 * Groups:
 *   1. Account state — Activate / Deactivate
 *   2. Security      — Lock / Unlock, Reset Password
 *   3. Invite        — Resend Invite (only when status === 'Invited')
 *
 * ── UX improvements over the previous version ────────────────────────────────
 *
 *   - Destructive actions (Deactivate, Lock) prompt a ConfirmDialog before
 *     executing, preventing accidental status changes.
 *   - Pending state: each button shows a spinner while its action is in flight.
 *   - Success / error inline feedback appears for 3.5 seconds.
 *   - Accessibility: all buttons have aria-label, aria-busy, focus-visible
 *     rings; confirm dialog handles Escape + focus management.
 *
 * ── Wiring guide ─────────────────────────────────────────────────────────────
 *
 *   When backend endpoints are ready:
 *   1. Create a BFF proxy at app/api/identity/admin/users/[id]/[action]/route.ts
 *   2. Replace each `await simulateAction()` stub with the actual fetch call.
 *   3. Call router.refresh() after success to re-fetch the Server Component.
 *
 * TODO: POST /identity/api/admin/users/{userId}/activate
 * TODO: POST /identity/api/admin/users/{userId}/deactivate
 * TODO: POST /identity/api/admin/users/{userId}/lock
 * TODO: POST /identity/api/admin/users/{userId}/unlock
 * TODO: POST /identity/api/admin/users/{userId}/reset-password
 * TODO: POST /identity/api/admin/users/{userId}/resend-invite
 */

import { useState }           from 'react';
import { useRouter }          from 'next/navigation';
import type { UserStatus }    from '@/types/control-center';
import { ConfirmDialog }      from '@/components/ui/confirm-dialog';
import { track }              from '@/lib/analytics';

interface UserActionsProps {
  userId:        string;
  currentStatus: UserStatus;
  isLocked?:     boolean;
}

type UserAction =
  | 'activate'
  | 'deactivate'
  | 'lock'
  | 'unlock'
  | 'reset-password'
  | 'resend-invite';

interface FeedbackState {
  type:    'success' | 'error';
  message: string;
}

const ACTION_LABELS: Record<UserAction, string> = {
  'activate':      'Activated',
  'deactivate':    'Deactivated',
  'lock':          'Locked',
  'unlock':        'Unlocked',
  'reset-password':'Password reset email sent',
  'resend-invite': 'Invitation resent',
};

const WIRED_ACTIONS = new Set<UserAction>(['activate', 'deactivate', 'resend-invite']);

export function UserActions({ userId, currentStatus, isLocked = false }: UserActionsProps) {
  const router = useRouter();

  const [confirming, setConfirming] = useState<UserAction | null>(null);
  const [pending, setPending]       = useState<UserAction | null>(null);
  const [feedback, setFeedback]     = useState<FeedbackState | null>(null);

  const isActive   = currentStatus === 'Active';
  const isInactive = currentStatus === 'Inactive';
  const isInvited  = currentStatus === 'Invited';

  function showFeedback(type: 'success' | 'error', message: string) {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 3500);
  }

  async function executeAction(action: UserAction) {
    setConfirming(null);
    setPending(action);

    try {
      if (WIRED_ACTIONS.has(action)) {
        const res = await fetch(
          `/api/identity/admin/users/${encodeURIComponent(userId)}/${action}`,
          { method: 'POST' },
        );
        if (!res.ok) {
          const body = await res.json().catch(() => ({})) as { message?: string };
          throw new Error(body.message ?? 'Action failed');
        }
        router.refresh();
      } else {
        await simulateAction(action);
      }

      const trackEvent: Record<UserAction, string> = {
        'activate':      'user.status.change',
        'deactivate':    'user.status.change',
        'lock':          'user.lock',
        'unlock':        'user.unlock',
        'reset-password':'user.password.reset',
        'resend-invite': 'user.invite.resend',
      };
      track(trackEvent[action] as import('@/lib/analytics').TrackEvent, { userId, action });
      showFeedback('success', `${ACTION_LABELS[action]}.`);
    } catch (err) {
      const msg = err instanceof Error ? err.message : `Failed to ${action.replace(/-/g, ' ')}.`;
      showFeedback('error', `${msg} Please try again.`);
    } finally {
      setPending(null);
    }
  }

  const isPendingAny = pending !== null;

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">

        {/* ── Account state ────────────────────────────────────────────── */}
        <ActionButton
          label="Activate"
          variant="success"
          disabled={isActive || isInvited || isPendingAny}
          isPending={pending === 'activate'}
          aria-label={isActive ? 'User is already active' : isInvited ? 'Accept the invitation first' : 'Activate this user'}
          title={isActive ? 'User is already active' : isInvited ? 'Accept the invitation first' : 'Activate this user'}
          onClick={() => executeAction('activate')}
        />
        <ActionButton
          label="Deactivate"
          variant="neutral"
          disabled={isInactive || isPendingAny}
          isPending={pending === 'deactivate'}
          aria-label={isInactive ? 'User is already inactive' : 'Deactivate this user'}
          title={isInactive ? 'User is already inactive' : 'Deactivate this user'}
          onClick={() => setConfirming('deactivate')}
        />

        {/* ── Divider ──────────────────────────────────────────────────── */}
        <div className="w-px h-5 bg-gray-200" aria-hidden />

        {/* ── Security ─────────────────────────────────────────────────── */}
        {isLocked ? (
          <ActionButton
            label="Unlock"
            variant="neutral"
            disabled={isPendingAny}
            isPending={pending === 'unlock'}
            aria-label="Unlock this user account"
            title="Unlock this user account"
            onClick={() => executeAction('unlock')}
          />
        ) : (
          <ActionButton
            label="Lock"
            variant="danger"
            disabled={isInvited || isPendingAny}
            isPending={pending === 'lock'}
            aria-label={isInvited ? 'Cannot lock a pending invitation' : 'Lock this user account'}
            title={isInvited ? 'Cannot lock a pending invitation' : 'Lock this user account'}
            onClick={() => setConfirming('lock')}
          />
        )}

        <ActionButton
          label="Reset Password"
          variant="neutral"
          disabled={isInvited || isPendingAny}
          isPending={pending === 'reset-password'}
          aria-label={isInvited ? 'User has not set a password yet' : 'Send a password reset email'}
          title={isInvited ? 'User has not set a password yet' : 'Send a password reset email'}
          onClick={() => executeAction('reset-password')}
        />

        {/* ── Invite ───────────────────────────────────────────────────── */}
        {isInvited && (
          <>
            <div className="w-px h-5 bg-gray-200" aria-hidden />
            <ActionButton
              label="Resend Invite"
              variant="primary"
              disabled={isPendingAny}
              isPending={pending === 'resend-invite'}
              aria-label="Resend the invitation email"
              title="Resend the invitation email"
              onClick={() => executeAction('resend-invite')}
            />
          </>
        )}

      </div>

      {/* Inline feedback */}
      {feedback && (
        <p
          role="status"
          aria-live="polite"
          className={`mt-2 text-xs font-medium ${
            feedback.type === 'success' ? 'text-green-700' : 'text-red-600'
          }`}
        >
          {feedback.message}
        </p>
      )}

      {/* Deactivate confirmation */}
      {confirming === 'deactivate' && (
        <ConfirmDialog
          title="Deactivate this user?"
          description="The user will immediately lose access to the platform. You can reactivate the account at any time."
          confirmLabel="Deactivate"
          variant="warning"
          isPending={pending === 'deactivate'}
          onConfirm={() => executeAction('deactivate')}
          onCancel={() => setConfirming(null)}
        />
      )}

      {/* Lock confirmation */}
      {confirming === 'lock' && (
        <ConfirmDialog
          title="Lock this user account?"
          description="The user will be signed out immediately and blocked from signing in. All active sessions will be terminated."
          confirmLabel="Lock Account"
          variant="danger"
          isPending={pending === 'lock'}
          onConfirm={() => executeAction('lock')}
          onCancel={() => setConfirming(null)}
        />
      )}
    </div>
  );
}

// ── Internal helpers ──────────────────────────────────────────────────────────

type ButtonVariant = 'primary' | 'success' | 'neutral' | 'danger';

function ActionButton({
  label,
  variant,
  disabled,
  isPending,
  title,
  onClick,
  'aria-label': ariaLabel,
}: {
  label:      string;
  variant:    ButtonVariant;
  disabled:   boolean;
  isPending:  boolean;
  title:      string;
  onClick:    () => void;
  'aria-label'?: string;
}) {
  const base = [
    'text-sm font-medium px-3 py-1.5 rounded-md border transition-colors',
    'disabled:opacity-40 disabled:cursor-not-allowed',
    'focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
  ].join(' ');

  const styles: Record<ButtonVariant, string> = {
    primary: 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700 focus-visible:ring-indigo-500',
    success: 'bg-green-600 text-white border-green-600 hover:bg-green-700 focus-visible:ring-green-500',
    neutral: 'bg-white text-gray-700 border-gray-200 hover:bg-gray-50 focus-visible:ring-gray-400',
    danger:  'bg-white text-red-600 border-red-200 hover:bg-red-50 focus-visible:ring-red-400',
  };

  return (
    <button
      type="button"
      disabled={disabled || isPending}
      title={title}
      aria-label={ariaLabel ?? label}
      aria-busy={isPending}
      onClick={onClick}
      className={`${base} ${styles[variant]}`}
    >
      {isPending ? (
        <span className="flex items-center gap-1.5">
          <span
            aria-hidden="true"
            className="h-3.5 w-3.5 rounded-full border-2 border-current/40 border-t-transparent animate-spin"
          />
          {label}…
        </span>
      ) : (
        label
      )}
    </button>
  );
}

/**
 * simulateAction — stub that resolves after 800ms.
 * Remove once real BFF proxy routes are wired.
 * TODO: delete when POST /identity/api/admin/users/{id}/{action} is live.
 */
async function simulateAction(_action: string): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, 800));
}
