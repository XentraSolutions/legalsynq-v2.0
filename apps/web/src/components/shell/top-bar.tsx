'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useState, useRef, useEffect } from 'react';
import { useSession } from '@/hooks/use-session';
import { orgTypeLabel } from '@/lib/nav';

/**
 * Full-width navy top bar.
 * Left:  LegalSynq logo
 * Right: avatar → profile dropdown
 */
export function TopBar() {
  const { session, clearSession } = useSession();

  return (
    <header
      className="flex items-center h-14 px-5 shrink-0 gap-4"
      style={{ backgroundColor: '#0f1928' }}
    >
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
            <ProfileMenuItem href="/profile"  icon="ri-user-3-line"     label="Profile"           onClick={() => setOpen(false)} />
            <ProfileMenuItem href="/settings" icon="ri-settings-3-line"  label="Account Settings" onClick={() => setOpen(false)} />
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
