'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useState, useRef, useEffect } from 'react';
import { useSession } from '@/hooks/use-session';
import { orgTypeLabel } from '@/lib/nav';

// ── All platform products shown in the app switcher ──────────────────────────

const ALL_PRODUCTS = [
  {
    id: 'careconnect',
    label: 'Synq CareConnect',
    href: '/careconnect/referrals',
    icon: 'ri-shield-cross-line',
    color: '#2563eb',         // blue
    bg:    '#eff6ff',
  },
  {
    id: 'fund',
    label: 'Synq Funds',
    href: '/fund/applications',
    icon: 'ri-bank-line',
    color: '#16a34a',         // green
    bg:    '#f0fdf4',
  },
  {
    id: 'lien',
    label: 'Synq Liens',
    href: '/lien/marketplace',
    icon: 'ri-file-stack-line',
    color: '#7c3aed',         // purple
    bg:    '#f5f3ff',
  },
  {
    id: 'ai',
    label: 'Synq AI',
    href: '#',
    icon: 'ri-robot-line',
    color: '#d97706',         // amber
    bg:    '#fffbeb',
  },
  {
    id: 'insights',
    label: 'Synq Insights',
    href: '#',
    icon: 'ri-bar-chart-2-line',
    color: '#0891b2',         // cyan
    bg:    '#ecfeff',
  },
] as const;

/**
 * Full-width navy top bar.
 * Left:   9-dot app-switcher → logo
 * Right:  avatar → profile dropdown
 */
export function TopBar() {
  const { session, clearSession } = useSession();

  return (
    <header
      className="flex items-center h-14 px-4 shrink-0 gap-3"
      style={{ backgroundColor: '#0f1928' }}
    >
      {/* ── App switcher ────────────────────────────────────────────────── */}
      <AppSwitcher />

      {/* ── Vertical divider ────────────────────────────────────────────── */}
      <div className="self-center h-5 w-px shrink-0" style={{ backgroundColor: 'rgba(255,255,255,0.15)' }} />

      {/* ── Logo ────────────────────────────────────────────────────────── */}
      <Link href="/dashboard" className="flex items-center shrink-0">
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

      {/* ── Spacer ──────────────────────────────────────────────────────── */}
      <div className="flex-1" />

      {/* ── User menu ───────────────────────────────────────────────────── */}
      {session && <UserMenu session={session} clearSession={clearSession} />}
    </header>
  );
}

// ── App switcher button + popout ──────────────────────────────────────────────

function AppSwitcher() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen(p => !p)}
        title="Switch product"
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          'w-8 h-8 flex items-center justify-center rounded-lg transition-colors',
          open
            ? 'bg-white/15 text-white'
            : 'text-slate-400 hover:bg-white/10 hover:text-white',
        ].join(' ')}
      >
        <i className="ri-apps-2-line text-[18px] leading-none" />
      </button>

      {open && (
        <div className="absolute left-0 top-[calc(100%+10px)] w-64 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
          {/* Header */}
          <div className="px-4 py-3 border-b border-gray-100">
            <p className="text-[11px] font-semibold uppercase tracking-widest text-gray-400">
              LegalSynq Products
            </p>
          </div>

          {/* Product list */}
          <div className="py-2">
            {ALL_PRODUCTS.map(product => (
              <Link
                key={product.id}
                href={product.href}
                onClick={() => setOpen(false)}
                className="flex items-center gap-3 px-4 py-2.5 hover:bg-gray-50 transition-colors group"
              >
                {/* Colored icon tile */}
                <div
                  className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0"
                  style={{ backgroundColor: product.bg }}
                >
                  <i
                    className={`${product.icon} text-[18px] leading-none`}
                    style={{ color: product.color }}
                  />
                </div>
                <span className="text-sm font-medium text-gray-700 group-hover:text-gray-900 transition-colors">
                  {product.label}
                </span>
              </Link>
            ))}
          </div>
        </div>
      )}
    </div>
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

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  async function handleSignOut() {
    setOpen(false);
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  const initials = session.orgName
    ? session.orgName.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase()
    : session.email.slice(0, 2).toUpperCase();

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen(p => !p)}
        className="flex items-center focus:outline-none group"
        aria-haspopup="true"
        aria-expanded={open}
      >
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 ring-2 ring-transparent group-hover:ring-white/20 transition-all"
          style={{ backgroundColor: '#f97316' }}
        >
          {initials}
        </div>
      </button>

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

          <div className="py-1.5">
            <ProfileMenuItem href="/profile"  icon="ri-user-3-line"    label="Profile"           onClick={() => setOpen(false)} />
            <ProfileMenuItem href="/settings" icon="ri-settings-3-line" label="Account Settings" onClick={() => setOpen(false)} />
          </div>

          <div className="border-t border-gray-100" />

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

function ProfileMenuItem({
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
