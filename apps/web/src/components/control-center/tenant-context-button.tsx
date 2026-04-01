'use client';

import { useRouter }              from 'next/navigation';
import { useTransition }          from 'react';
import {
  setNotifTenantContextAction,
  clearNotifTenantContextAction,
} from '@/app/(control-center)/control-center/notifications/actions';
import type { TenantContext } from '@/app/(control-center)/control-center/notifications/actions';

// ── Activate button ────────────────────────────────────────────────────────────

interface ActivateTenantButtonProps {
  tenant: TenantContext;
}

export function ActivateTenantButton({ tenant }: ActivateTenantButtonProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function handleActivate() {
    startTransition(async () => {
      await setNotifTenantContextAction(tenant);
      router.refresh();
    });
  }

  return (
    <button
      type="button"
      onClick={handleActivate}
      disabled={isPending}
      className="text-xs text-amber-700 font-medium border border-amber-300 bg-amber-50 hover:bg-amber-100 disabled:opacity-50 disabled:cursor-not-allowed px-2.5 py-1 rounded-md transition-colors whitespace-nowrap"
    >
      {isPending ? 'Activating…' : 'Set Active'}
    </button>
  );
}

// ── Clear / deactivate button ──────────────────────────────────────────────────

export function ClearTenantContextButton({ label = 'Change Tenant' }: { label?: string }) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function handleClear() {
    startTransition(async () => {
      await clearNotifTenantContextAction();
      router.refresh();
    });
  }

  return (
    <button
      type="button"
      onClick={handleClear}
      disabled={isPending}
      className="text-xs text-gray-500 border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed px-3 py-1.5 rounded-md transition-colors whitespace-nowrap"
    >
      {isPending ? 'Clearing…' : label}
    </button>
  );
}
