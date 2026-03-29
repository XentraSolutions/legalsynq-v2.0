'use client';

import type { UserStatus } from '@/types/control-center';

interface UserActionsProps {
  userId:        string;
  currentStatus: UserStatus;
  isLocked?:     boolean;
}

/**
 * User action buttons — Client Component.
 *
 * Groups:
 *   1. Account state — Activate / Deactivate
 *   2. Security      — Lock / Unlock, Reset Password
 *   3. Invite        — Resend Invite (only when status === 'Invited')
 *
 * All actions are UI-only placeholders. See TODO comments for wiring instructions.
 *
 * TODO: Create a BFF proxy at app/api/identity/admin/users/[id]/[action]/route.ts
 *       Then call it here with fetch + router.refresh() on success.
 */
export function UserActions({ userId: _userId, currentStatus, isLocked = false }: UserActionsProps) {
  const isActive   = currentStatus === 'Active';
  const isInactive = currentStatus === 'Inactive';
  const isInvited  = currentStatus === 'Invited';

  return (
    <div className="flex flex-wrap items-center gap-2">
      {/* ── Account state ─────────────────────────────────────────────── */}
      <ActionButton
        label="Activate"
        variant="success"
        disabled={isActive || isInvited}
        title={isActive ? 'User is already active' : isInvited ? 'Accept the invitation first' : 'Activate this user'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/users/{userId}/activate
          alert('Activate: backend integration pending');
        }}
      />
      <ActionButton
        label="Deactivate"
        variant="neutral"
        disabled={isInactive}
        title={isInactive ? 'User is already inactive' : 'Deactivate this user'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/users/{userId}/deactivate
          alert('Deactivate: backend integration pending');
        }}
      />

      {/* ── Divider ───────────────────────────────────────────────────── */}
      <div className="w-px h-5 bg-gray-200" aria-hidden />

      {/* ── Security ──────────────────────────────────────────────────── */}
      {isLocked ? (
        <ActionButton
          label="Unlock"
          variant="neutral"
          disabled={false}
          title="Unlock this user account"
          onClick={() => {
            // TODO: POST /api/identity/api/admin/users/{userId}/unlock
            alert('Unlock: backend integration pending');
          }}
        />
      ) : (
        <ActionButton
          label="Lock"
          variant="danger"
          disabled={isInvited}
          title={isInvited ? 'Cannot lock a pending invitation' : 'Lock this user account'}
          onClick={() => {
            // TODO: POST /api/identity/api/admin/users/{userId}/lock
            alert('Lock: backend integration pending');
          }}
        />
      )}
      <ActionButton
        label="Reset Password"
        variant="neutral"
        disabled={isInvited}
        title={isInvited ? 'User has not set a password yet' : 'Send a password reset email'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/users/{userId}/reset-password
          alert('Reset Password: backend integration pending');
        }}
      />

      {/* ── Invite ────────────────────────────────────────────────────── */}
      {isInvited && (
        <>
          <div className="w-px h-5 bg-gray-200" aria-hidden />
          <ActionButton
            label="Resend Invite"
            variant="primary"
            disabled={false}
            title="Resend the invitation email"
            onClick={() => {
              // TODO: POST /api/identity/api/admin/users/{userId}/resend-invite
              alert('Resend Invite: backend integration pending');
            }}
          />
        </>
      )}
    </div>
  );
}

// ── Internal ──────────────────────────────────────────────────────────────────

type ButtonVariant = 'primary' | 'success' | 'neutral' | 'danger';

function ActionButton({
  label,
  variant,
  disabled,
  title,
  onClick,
}: {
  label:    string;
  variant:  ButtonVariant;
  disabled: boolean;
  title:    string;
  onClick:  () => void;
}) {
  const base = 'text-sm font-medium px-3 py-1.5 rounded-md border transition-colors disabled:opacity-40 disabled:cursor-not-allowed';
  const styles: Record<ButtonVariant, string> = {
    primary: 'bg-indigo-600 text-white border-indigo-600 hover:bg-indigo-700',
    success: 'bg-green-600 text-white border-green-600 hover:bg-green-700',
    neutral: 'bg-white text-gray-700 border-gray-200 hover:bg-gray-50',
    danger:  'bg-white text-red-600 border-red-200 hover:bg-red-50',
  };

  return (
    <button
      type="button"
      disabled={disabled}
      title={title}
      onClick={onClick}
      className={`${base} ${styles[variant]}`}
    >
      {label}
    </button>
  );
}
