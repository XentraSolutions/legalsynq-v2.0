'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  ShieldCheck, Banknote, FileStack, Settings,
  FileText, Calendar, MapPin, FilePlus,
  ShoppingBag, Layers, Briefcase,
  Users, Building2, Package, Globe,
  LogOut, UserCircle,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useSession } from '@/hooks/use-session';
import { useProduct } from '@/contexts/product-context';
import { orgTypeLabel } from '@/lib/nav';
import type { NavItem } from '@/types';
import { clsx } from 'clsx';

// ── Product branding icons ────────────────────────────────────────────────────

const PRODUCT_ICONS: Record<string, LucideIcon> = {
  careconnect: ShieldCheck,
  fund:        Banknote,
  lien:        FileStack,
  admin:       Settings,
};

const PRODUCT_DISPLAY: Record<string, string> = {
  careconnect: 'Synq CareConnect',
  fund:        'Synq Fund',
  lien:        'Synq Lien',
  admin:       'Administration',
};

// ── Nav item icons by href ────────────────────────────────────────────────────

const NAV_ICONS: Record<string, LucideIcon> = {
  '/careconnect/referrals':    FileText,
  '/careconnect/appointments': Calendar,
  '/careconnect/providers':    MapPin,
  '/fund/applications':        FileStack,
  '/fund/applications/new':    FilePlus,
  '/lien/marketplace':         ShoppingBag,
  '/lien/my-liens':            Layers,
  '/lien/my-liens/new':        FilePlus,
  '/lien/portfolio':           Briefcase,
  '/admin/users':              Users,
  '/admin/organizations':      Building2,
  '/admin/products':           Package,
  '/admin/tenants':            Globe,
};

// ── Sidebar ───────────────────────────────────────────────────────────────────

export function Sidebar() {
  const { session, clearSession } = useSession();
  const pathname = usePathname();
  const { activeGroup } = useProduct();

  async function handleSignOut() {
    await fetch('/api/auth/logout', { method: 'POST' });
    clearSession();
    window.location.href = '/login';
  }

  const productId      = activeGroup?.id ?? '';
  const ProductIcon    = PRODUCT_ICONS[productId] ?? ShieldCheck;
  const productDisplay = PRODUCT_DISPLAY[productId] ?? (activeGroup?.label ?? 'LegalSynq');

  return (
    <aside
      className="w-[215px] shrink-0 flex flex-col h-full overflow-hidden"
      style={{ backgroundColor: '#0f1928' }}
    >
      {/* ── Product branding ──────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 px-5 py-5 shrink-0">
        <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center shrink-0">
          <ProductIcon size={16} className="text-white" strokeWidth={2.5} />
        </div>
        <span className="text-white font-semibold text-sm leading-tight">
          {productDisplay}
        </span>
      </div>

      {/* ── Nav items ─────────────────────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto px-3">
        {activeGroup && (
          <>
            <p className="text-[10px] font-semibold uppercase tracking-widest text-slate-500 px-3 mb-2">
              Menu
            </p>
            <nav className="space-y-0.5">
              {activeGroup.items.map(item => (
                <SidebarItem key={item.href} item={item} pathname={pathname} />
              ))}
            </nav>
          </>
        )}
      </div>

      {/* ── User profile + sign out ───────────────────────────────────────── */}
      {session && (
        <div className="shrink-0 border-t px-4 py-4" style={{ borderColor: 'rgba(255,255,255,0.08)' }}>
          <div className="flex items-center gap-3 mb-3">
            <div
              className="w-8 h-8 rounded-full flex items-center justify-center shrink-0"
              style={{ backgroundColor: '#1e2f42' }}
            >
              <UserCircle size={20} className="text-slate-300" />
            </div>
            <div className="min-w-0">
              <p className="text-sm font-medium text-white truncate leading-snug">
                {session.orgName ?? session.email}
              </p>
              <p className="text-xs text-slate-400 truncate leading-snug">
                {orgTypeLabel(session.orgType)}
              </p>
            </div>
          </div>

          <button
            onClick={handleSignOut}
            className="flex items-center gap-2 text-xs text-slate-400 hover:text-white transition-colors"
          >
            <LogOut size={13} />
            <span>Sign Out</span>
          </button>
        </div>
      )}
    </aside>
  );
}

// ── Nav item ──────────────────────────────────────────────────────────────────

function SidebarItem({ item, pathname }: { item: NavItem; pathname: string }) {
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');
  const Icon     = NAV_ICONS[item.href];

  return (
    <Link
      href={item.href}
      className={clsx(
        'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
        isActive
          ? 'bg-blue-600 text-white'
          : 'text-slate-400 hover:text-white hover:bg-white/5',
      )}
    >
      {Icon && <Icon size={16} strokeWidth={1.75} />}
      <span>{item.label}</span>
    </Link>
  );
}
