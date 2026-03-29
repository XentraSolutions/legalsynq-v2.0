'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { useProduct } from '@/contexts/product-context';
import { PRODUCT_DEFS } from '@/lib/product-config';
import { orgTypeLabel } from '@/lib/nav';

/**
 * Full-width navy top bar.
 * Left:   LegalSynq white logo
 * Center: product switcher tabs (only accessible products shown)
 * Right:  org name / user info + sign-out
 */
export function TopBar() {
  const { session, clearSession } = useSession();
  const { activeProductId, availableGroups } = useProduct();

  async function handleSignOut() {
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  const accessibleProducts = PRODUCT_DEFS.filter(def =>
    availableGroups.some(g => g.id === def.id)
  );

  return (
    <header
      className="flex items-stretch h-14 px-5 shrink-0 gap-4"
      style={{ backgroundColor: '#0f1928' }}
    >
      {/* ── Logo ──────────────────────────────────────────────────────────── */}
      <Link href="/dashboard" className="flex items-center shrink-0 mr-2">
        <Image
          src="/legalsynq-logo-white.png"
          alt="LegalSynq"
          width={130}
          height={32}
          priority
          className="h-7 w-auto"
        />
      </Link>

      {/* ── Vertical divider ──────────────────────────────────────────────── */}
      <div className="self-center h-5 w-px shrink-0" style={{ backgroundColor: 'rgba(255,255,255,0.12)' }} />

      {/* ── Product switcher ──────────────────────────────────────────────── */}
      <nav className="flex items-stretch gap-0.5 flex-1">
        {accessibleProducts.map(def => {
          const isActive = activeProductId === def.id;
          return (
            <Link
              key={def.id}
              href={def.routePrefix}
              className={[
                'flex items-center gap-1.5 px-3.5 text-xs font-medium transition-colors border-b-2 select-none',
                isActive
                  ? 'text-white border-orange-500'
                  : 'text-slate-400 hover:text-white border-transparent hover:border-white/20',
              ].join(' ')}
            >
              <i
                className={`${def.riIcon} text-sm leading-none`}
                style={{ color: isActive ? '#f97316' : 'inherit' }}
              />
              <span>{def.label}</span>
            </Link>
          );
        })}
      </nav>

      {/* ── User area ─────────────────────────────────────────────────────── */}
      {session && (
        <div className="flex items-center gap-3 shrink-0">
          <div className="flex items-center gap-2">
            <div
              className="w-7 h-7 rounded-full flex items-center justify-center shrink-0"
              style={{ backgroundColor: 'rgba(255,255,255,0.1)' }}
            >
              <i className="ri-user-3-line text-[13px] text-slate-300" />
            </div>
            <div className="hidden sm:block leading-tight">
              <p className="text-xs font-semibold text-white">{session.orgName ?? session.email}</p>
              <p className="text-[10px] text-slate-400">{orgTypeLabel(session.orgType)}</p>
            </div>
          </div>

          <div className="h-4 w-px" style={{ backgroundColor: 'rgba(255,255,255,0.12)' }} />

          <button
            onClick={handleSignOut}
            title="Sign out"
            className="flex items-center gap-1.5 text-xs text-slate-400 hover:text-white transition-colors py-1 px-2 rounded-md hover:bg-white/5"
          >
            <i className="ri-logout-box-r-line text-sm" />
            <span className="hidden sm:inline">Sign out</span>
          </button>
        </div>
      )}
    </header>
  );
}
