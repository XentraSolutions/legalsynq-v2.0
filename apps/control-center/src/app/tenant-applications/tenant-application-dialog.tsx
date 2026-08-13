'use client';

import { ConfirmDialog } from '@/components/ui/confirm-dialog';

export type TenantApplicationDecision = 'approve' | 'decline';

export function TenantApplicationDialog({ tenantName, decision, pending, error, onConfirm, onCancel }: {
  tenantName: string;
  decision: TenantApplicationDecision;
  pending: boolean;
  error?: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const approving = decision === 'approve';

  return (
    <ConfirmDialog
      appearance="spacious"
      title={`${approving ? 'Accept' : 'Decline'} Tenant Application?`}
      description={
        <>
          You&apos;re about to {approving ? 'approve' : 'decline'} <strong className="font-semibold text-[#404040]">{tenantName}</strong> tenant application. Are you sure you want to continue?
          {error && <span role="alert" className="mt-2 block text-sm text-[#dc2626]">{error}</span>}
        </>
      }
      icon={<i className={approving ? 'ri-file-check-line' : 'ri-file-close-line'} aria-hidden="true" />}
      confirmLabel={approving ? 'Accept' : 'Decline'}
      variant={approving ? 'success' : 'danger'}
      isPending={pending}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
