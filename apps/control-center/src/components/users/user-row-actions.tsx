'use client';

/**
 * UserRowActions — compact inline action buttons for a user list table row.
 *
 * Actions wired to live BFF:
 *   - Activate   → POST /api/identity/admin/users/{id}/activate
 *   - Deactivate → POST /api/identity/admin/users/{id}/deactivate  (confirm dialog)
 *   - Resend     → POST /api/identity/admin/users/{id}/resend-invite
 *
 * After a successful action the page is refreshed via router.refresh() so the
 * server component re-fetches updated data from the identity service.
 */

import { useState }        from 'react';
import { useRouter }       from 'next/navigation';
import Link                from 'next/link';
import type { UserStatus } from '@/types/control-center';
import { Routes }          from '@/lib/routes';

interface UserRowActionsProps {
  userId:        string;
  currentStatus: UserStatus;
}

type RowAction = 'activate' | 'deactivate' | 'resend-invite';

export function UserRowActions({ userId, currentStatus }: UserRowActionsProps) {
  const router = useRouter();

  const [pending,    setPending]    = useState<RowAction | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [error,      setError]      = useState<string | null>(null);

  const isActive   = currentStatus === 'Active';
  const isInactive = currentStatus === 'Inactive';
  const isInvited  = currentStatus === 'Invited';

  async function run(action: RowAction) {
    setError(null);
    setPending(action);
    try {
      const res = await fetch(
        `/api/identity/admin/users/${encodeURIComponent(userId)}/${action}`,
        { method: 'POST' },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({})) as { message?: string };
        throw new Error(body.message ?? 'Action failed.');
      }
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred.');
    } finally {
      setPending(null);
      setConfirming(false);
    }
  }

  return (
    <div className="flex items-center gap-1.5 flex-wrap">

      {/* View detail */}
      <Link
        href={Routes.userDetail(userId)}
        className="text-xs px-2 py-1 rounded border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 hover:text-gray-900 transition-colors whitespace-nowrap"
      >
        View
      </Link>

      {/* Activate — shown for Inactive or Invited */}
      {(isInactive || isInvited) && (
        <ActionButton
          label="Activate"
          isPending={pending === 'activate'}
          variant="success"
          onClick={() => run('activate')}
        />
      )}

      {/* Deactivate — shown for Active; requires confirmation */}
      {isActive && !confirming && (
        <ActionButton
          label="Deactivate"
          isPending={false}
          variant="danger"
          onClick={() => setConfirming(true)}
        />
      )}

      {/* Inline confirm prompt */}
      {isActive && confirming && (
        <span className="inline-flex items-center gap-1 text-xs">
          <span className="text-red-700 font-medium">Deactivate?</span>
          <button
            type="button"
            onClick={() => run('deactivate')}
            disabled={pending === 'deactivate'}
            className="px-2 py-0.5 rounded bg-red-600 text-white text-[11px] font-medium hover:bg-red-700 disabled:opacity-50 transition-colors"
          >
            {pending === 'deactivate' ? '…' : 'Yes'}
          </button>
          <button
            type="button"
            onClick={() => setConfirming(false)}
            className="px-2 py-0.5 rounded border border-gray-200 bg-white text-gray-500 text-[11px] hover:bg-gray-50 transition-colors"
          >
            No
          </button>
        </span>
      )}

      {/* Resend Invite — only for Invited status */}
      {isInvited && (
        <ActionButton
          label="Resend"
          isPending={pending === 'resend-invite'}
          variant="neutral"
          onClick={() => run('resend-invite')}
        />
      )}

      {/* Inline error */}
      {error && (
        <span className="text-[11px] text-red-600" title={error}>
          ⚠ {error.length > 30 ? error.slice(0, 28) + '…' : error}
        </span>
      )}
    </div>
  );
}

// ── Internal helpers ──────────────────────────────────────────────────────────

type Variant = 'success' | 'danger' | 'neutral';

const variantClass: Record<Variant, string> = {
  success: 'border-green-200 bg-white text-green-700 hover:bg-green-50',
  danger:  'border-red-200 bg-white text-red-600 hover:bg-red-50',
  neutral: 'border-gray-200 bg-white text-gray-600 hover:bg-gray-50',
};

function ActionButton({
  label,
  isPending,
  variant,
  onClick,
}: {
  label:     string;
  isPending: boolean;
  variant:   Variant;
  onClick:   () => void;
}) {
  return (
    <button
      type="button"
      disabled={isPending}
      onClick={onClick}
      className={`text-xs px-2 py-1 rounded border transition-colors disabled:opacity-40 disabled:cursor-not-allowed whitespace-nowrap ${variantClass[variant]}`}
    >
      {isPending ? `${label}…` : label}
    </button>
  );
}
