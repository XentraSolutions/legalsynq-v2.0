'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useState, useRef, useEffect } from 'react';
import { useSession } from '@/hooks/use-session';
import { useProduct } from '@/contexts/product-context';
import { PRODUCT_DEFS } from '@/lib/product-config';
import { orgTypeLabel } from '@/lib/nav';

/**
 * Full-width navy top bar.
 * Left:   LegalSynq white logo
 * Center: product switcher tabs
 * Right:  avatar button → profile dropdown
 */
export function TopBar() {
  const { session, clearSession } = useSession();
  const { activeProductId, availableGroups } = useProduct();

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
          unoptimized
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

      {/* ── User menu ─────────────────────────────────────────────────────── */}
      {session && <UserMenu session={session} clearSession={clearSession} />}
    </header>
  );
}

// ── Profile dropdown ──────────────────────────────────────────────────────────

interface UserMenuProps {
  session: NonNullable<ReturnType<typeof useSession>['session']>;
  clearSession: () => void;
}

function UserMenu({ session, clearSession }: UserMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false);
    }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  async function handleSignOut() {
    setOpen(false);
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  // Initials for avatar
  const initials = session.orgName
    ? session.orgName.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase()
    : session.email.slice(0, 2).toUpperCase();

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen(prev => !prev)}
        className="flex items-center gap-2.5 rounded-full focus:outline-none group"
        aria-haspopup="true"
        aria-expanded={open}
      >
        {/* Avatar circle */}
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 ring-2 ring-transparent group-hover:ring-white/20 transition-all"
          style={{ backgroundColor: '#f97316' }}
        >
          {initials}
        </div>
      </button>

      {/* ── Dropdown panel ─────────────────────────────────────────────── */}
      {open && (
        <div
          className="absolute right-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-xl border border-gray-200 overflow-hidden z-50"
          role="menu"
        >
          {/* User header */}
          <div className="flex items-center gap-3 px-4 py-3.5 bg-gray-50 border-b border-gray-100">
            <div
              className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold text-white shrink-0"
              style={{ backgroundColor: '#f97316' }}
            >
              {initials}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-semibold text-gray-900 truncate">
                {session.orgName ?? session.email}
              </p>
              <p className="text-xs text-gray-500 truncate">{session.email}</p>
              <p className="text-[10px] text-gray-400 mt-0.5">{orgTypeLabel(session.orgType)}</p>
            </div>
          </div>

          {/* Menu items */}
          <div className="py-1.5">
            <MenuItem
              href="/profile"
              icon="ri-user-3-line"
              label="Profile"
              onClick={() => setOpen(false)}
            />
            <MenuItem
              href="/settings"
              icon="ri-settings-3-line"
              label="Account Settings"
              onClick={() => setOpen(false)}
            />
          </div>

          {/* Divider */}
          <div className="border-t border-gray-100" />

          {/* Sign out */}
          <div className="py-1.5">
            <button
              onClick={handleSignOut}
              role="menuitem"
              className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition-colors"
            >
              <i className="ri-logout-box-r-line text-base leading-none" />
              <span>Log out</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function MenuItem({
  href, icon, label, onClick,
}: { href: string; icon: string; label: string; onClick: () => void }) {
  return (
    <Link
      href={href}
      role="menuitem"
      onClick={onClick}
      className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
    >
      <i className={`${icon} text-base leading-none text-gray-400`} />
      <span>{label}</span>
    </Link>
  );
}
