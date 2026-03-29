'use client';

import type { TenantStatus } from '@/types/control-center';

interface TenantActionsProps {
  tenantId:      string;
  currentStatus: TenantStatus;
}

/**
 * Tenant action buttons — Client Component.
 *
 * Displays Activate, Deactivate, and Suspend buttons based on current status.
 * Buttons are UI-only for now — mutation wiring is behind TODO comments.
 *
 * TODO: wire Activate   → POST /api/identity/api/admin/tenants/{id}/activate
 * TODO: wire Deactivate → POST /api/identity/api/admin/tenants/{id}/deactivate
 * TODO: wire Suspend    → POST /api/identity/api/admin/tenants/{id}/suspend
 *
 * When wiring:
 *   1. Create a BFF proxy at app/api/identity/[...path]/route.ts (like apps/web pattern)
 *   2. Call the proxy from here using fetch('/api/identity/api/admin/...')
 *   3. On success: router.refresh() to re-fetch the Server Component
 */
export function TenantActions({ tenantId: _tenantId, currentStatus }: TenantActionsProps) {
  const isActive    = currentStatus === 'Active';
  const isInactive  = currentStatus === 'Inactive';
  const isSuspended = currentStatus === 'Suspended';

  return (
    <div className="flex items-center gap-2 shrink-0">
      {/* Activate — available when Inactive or Suspended */}
      <ActionButton
        label="Activate"
        variant="success"
        disabled={isActive}
        title={isActive ? 'Tenant is already active' : 'Activate this tenant'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/tenants/{tenantId}/activate
          alert('Activate: backend integration pending');
        }}
      />

      {/* Deactivate — available when Active */}
      <ActionButton
        label="Deactivate"
        variant="neutral"
        disabled={isInactive}
        title={isInactive ? 'Tenant is already inactive' : 'Deactivate this tenant'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/tenants/{tenantId}/deactivate
          alert('Deactivate: backend integration pending');
        }}
      />

      {/* Suspend — available when Active */}
      <ActionButton
        label="Suspend"
        variant="danger"
        disabled={isSuspended}
        title={isSuspended ? 'Tenant is already suspended' : 'Suspend this tenant'}
        onClick={() => {
          // TODO: POST /api/identity/api/admin/tenants/{tenantId}/suspend
          alert('Suspend: backend integration pending');
        }}
      />
    </div>
  );
}

// ── Internal ──────────────────────────────────────────────────────────────────

type ButtonVariant = 'success' | 'neutral' | 'danger';

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
