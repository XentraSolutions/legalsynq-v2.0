'use client';

import { useRouter }     from 'next/navigation';
import { useTransition } from 'react';

const COOKIE_NAME = 'cc_tenant_context';

interface SetActiveTenantButtonProps {
  tenantId:   string;
  tenantName: string;
  tenantCode: string;
}

/**
 * SetActiveTenantButton — writes cc_tenant_context client-side.
 *
 * The cookie is intentionally not HttpOnly so it can be written from JS.
 * Writing it here bypasses the Next.js Server Actions CSRF check, which
 * fires in the Replit dev environment due to a port mismatch between the
 * origin and x-forwarded-host headers.
 */
export function SetActiveTenantButton({ tenantId, tenantName, tenantCode }: SetActiveTenantButtonProps) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function handleActivate() {
    const value   = JSON.stringify({ tenantId, tenantName, tenantCode });
    const sameSite = process.env.NODE_ENV === 'production' ? 'Strict' : 'Lax';
    document.cookie = `${COOKIE_NAME}=${encodeURIComponent(value)}; path=/; SameSite=${sameSite}`;

    startTransition(() => {
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

export function ClearActiveTenantButton({ label = 'Deactivate' }: { label?: string }) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function handleClear() {
    document.cookie = `${COOKIE_NAME}=; path=/; max-age=0`;

    startTransition(() => {
      router.refresh();
    });
  }

  return (
    <button
      type="button"
      onClick={handleClear}
      disabled={isPending}
      className="text-xs text-gray-500 border border-gray-200 bg-white hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed px-2.5 py-1 rounded-md transition-colors whitespace-nowrap"
    >
      {isPending ? 'Clearing…' : label}
    </button>
  );
}
